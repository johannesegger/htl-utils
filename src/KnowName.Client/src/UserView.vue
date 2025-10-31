<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { Result, uiFetch, Workflow } from './UIFetch'
import * as DataTransfer from './user/DataTransfer'
import * as Types from './user/Types'
import ErrorWithRetry from './ErrorWithRetry.vue'
import GroupSelection from './user/GroupSelection.vue'
import { shuffle } from 'lodash-es'
import SelectablePerson from './user/SelectablePerson.vue'
import { PersonImage } from './PersonImage'

const showGroups = ref(false)
const totalGuesses = ref(0)
const correctGuesses = ref(0)
const correctGuessRatio = computed(() => correctGuesses.value / totalGuesses.value)
const points = computed(() => correctGuesses.value - (totalGuesses.value - correctGuesses.value))
const pointColorClasses = computed(() => {
  if (totalGuesses.value === 0) return ""
  if (correctGuessRatio.value > 0.9) return "text-green-500"
  if (correctGuessRatio.value > 0.5) return "text-yellow-500"
  return "text-red-500"
})
type Guess = {
  id: number
  personToGuess: Person
  correct: boolean
}
let nextGuessId = 1
const guesses = ref<Guess[]>([])
const resetGuesses = () => {
  totalGuesses.value = 0
  correctGuesses.value = 0
  guesses.value = []
}

type LoadGroupsError = 'not-authorized'
const loadGroupsWorkflow = Workflow.init(async () : Promise<Result<DataTransfer.PersonGroup[][], LoadGroupsError>> => {
  const response = await uiFetch('/api/person/groups')
  if (response.ok) {
    const loadedGroups : DataTransfer.PersonGroup[][] = await response.json()
    return Result.ok(loadedGroups)
  }
  else if (response.status === 403) {
    return Result.error('not-authorized')
  }
  else {
    throw response
  }
})
loadGroupsWorkflow.run()
const loadGroupsErrorMessage = computed(() => {
  if (loadGroupsWorkflow.isRunning.value === true || loadGroupsWorkflow.result.value === undefined) return undefined
  if (loadGroupsWorkflow.result.value.succeeded === true) return undefined
  if (loadGroupsWorkflow.result.value.error.type === 'expected') {
    switch (loadGroupsWorkflow.result.value.error.error) {
      case 'not-authorized': return 'Fehler beim Laden der Gruppen. Sie sind nicht berechtigt.'
    }
  }
  return 'Unerwarteter Fehler beim Laden der Gruppen.'
})

const selectedGroups = ref([] as Types.SelectedPersonGroup[][])
watch(loadGroupsWorkflow.result, loadedGroups => {
  if (loadedGroups === undefined || loadedGroups.succeeded === false) return

  // TODO blend with currently selected groups
  selectedGroups.value = loadedGroups.result.map(groups =>
    groups.map((group) : Types.SelectedPersonGroup => ({
      displayName: group.displayName,
      persons: group.persons,
      selectablePersons: group.persons.filter(v => v.imageUrl !== null).length,
      isSelected: false,
    }))
  )
}, { deep: true })

type Person = {
  id: number
  groupName: string
  displayName: string
  image: PersonImage
  isSelected: boolean
}

let nextPersonId = 1
const allPersons = computed(() => {
  return selectedGroups.value
    .flatMap(v => v)
    .filter(v => v.isSelected)
    .flatMap(group => group.persons.map((person) : Person => (
      {
        id: nextPersonId++,
        groupName: group.displayName,
        displayName: person.displayName,
        image: PersonImage.fromLink(person.imageUrl),
        isSelected: true,
      }
    )))
})

const remainingPersons = ref([] as Person[])
const nextPerson = ref<Person>()
watch(allPersons, allPersons => {
  remainingPersons.value = shuffle(allPersons.filter(v => v.image != undefined))
  nextPerson.value = remainingPersons.value.pop()
})

watch(nextPerson, async nextPerson => {
  if (nextPerson === undefined) return

  nextPerson.image = await PersonImage.load(nextPerson.image)
})

const nameFilter = ref('')

const filteredPersons = computed(() => {
  let nameParts = nameFilter.value.toLowerCase().split(' ')
  return allPersons.value
    .filter(v => nameParts.every(substring =>
      v.displayName.toLowerCase().indexOf(substring) != -1
    ))
})

const selectablePersons = computed(() => {
  return filteredPersons.value.filter(v => v.image !== undefined)
})

const selectedPerson = ref<Person>()
watch(selectablePersons, selectablePersons => {
  if (selectedPerson.value === undefined || !selectablePersons.map(v => v.id).includes(selectedPerson.value.id)) {
    selectedPerson.value = selectablePersons[0]
  }
})

const selectNextPerson = (indexStep: number) => {
  if (selectedPerson.value === undefined) {
    selectedPerson.value = selectablePersons.value[0]
    return
  }
  const selectedPersonIndex = selectablePersons.value.map(v => v.id).indexOf(selectedPerson.value.id)
  let nextPersonIndex = (selectedPersonIndex + indexStep + selectablePersons.value.length) % selectablePersons.value.length
  selectedPerson.value = selectablePersons.value[nextPersonIndex]
}

const submitGuess = async (person: Person | undefined = undefined) => {
  if (nextPerson.value === undefined) return

  person = person || selectedPerson.value

  const guessCorrect = nextPerson.value.id === person?.id
  totalGuesses.value++
  if (guessCorrect) {
    correctGuesses.value++
  }
  guesses.value.push({ id: nextGuessId++, personToGuess: nextPerson.value, correct: guessCorrect })

  nameFilter.value = ''

  await nextTick()

  selectedPerson.value = selectablePersons.value[0]
  if (remainingPersons.value.length > 0) {
    nextPerson.value = remainingPersons.value.pop()
  }
  else {
    const newRemainingPersons = shuffle(selectablePersons.value)
    const newNextPerson = newRemainingPersons.pop()

    nextPerson.value = newNextPerson
    remainingPersons.value = newRemainingPersons
  }
}

const filterElem = useTemplateRef('filter-input')

const onKeyDown = (e: KeyboardEvent) => {
  switch (e.key) {
    case 'ArrowDown':
      selectNextPerson(+1)
      e.preventDefault()
      break
    case 'ArrowUp':
      selectNextPerson(-1)
      e.preventDefault()
      break
    case 'Enter':
      submitGuess();
      e.preventDefault()
      break
  }
  filterElem.value?.focus()
}
document.addEventListener('keydown', onKeyDown)
onUnmounted(() => document.removeEventListener('keydown', onKeyDown))
</script>

<template>
  <header class="grid grid-cols-3 items-center p-8 bg-amber-200">
    <div class="justify-self-start flex flex-col sm:flex-row items-center gap-2">
      <img src="/logo.svg" width="32px" height="32px" />
      <h1 class="text-lg sm:text-3xl text-center small-caps">Know Name</h1>
    </div>
    <div class="justify-self-center">
      <button class="btn max-sm:text-sm"
        :class="{
          'bg-amber-50': showGroups === false,
          'bg-amber-300': showGroups === true
        }"
        @click="showGroups = !showGroups">Gruppen auswählen</button>
    </div>
    <div class="justify-self-end flex flex-col items-center">
      <span class="text-sm text-black/75">Punkte</span>
      <span class="text-3xl font-bold select-none" :class="pointColorClasses" @dblclick="resetGuesses">{{ points }}</span>
    </div>
  </header>
  <main class="grow flex flex-col gap-4 p-4 min-h-0">
    <div v-if="showGroups === true">
      <div v-if="loadGroupsWorkflow.isRunning.value === true">Gruppen werden geladen...</div>
      <ErrorWithRetry v-if="loadGroupsErrorMessage !== undefined" @retry="loadGroupsWorkflow.run">{{ loadGroupsErrorMessage }}</ErrorWithRetry>
      <GroupSelection v-if="loadGroupsWorkflow.result.value?.succeeded" v-model="selectedGroups" />
    </div>
    <div v-if="allPersons.length > 0" class="grow grid grid-cols-[2fr_1fr] gap-2 min-h-50">
      <div class="grow shadow-xl/30 p-4 min-h-0">
        <img v-if="nextPerson?.image?.type === 'blob'"
          :src="nextPerson.image.url"
          class="object-contain w-full h-full" />
      </div>
      <div class="flex flex-col gap-2 px-2 py-4 shadow-xl/30 min-h-0">
        <input ref="filter-input" class="input-text" placeholder="Name" v-model="nameFilter" />
        <div class="overflow-y-auto">
          <SelectablePerson v-for="person in filteredPersons" :key="person.id"
            :display-name="person.displayName"
            :group-name="person.groupName"
            :is-selectable="person.image !== undefined"
            :is-selected="person.id === selectedPerson?.id"
            @select="submitGuess(person)" />
        </div>
      </div>
    </div>
    <div class="flex justify-end gap-1">
      <div v-for="guess in guesses" class="rounded flex flex-col items-center p-2 gap-1 min-w-fit" :class="{ 'bg-green-100': guess.correct === true, 'bg-red-100': guess.correct === false }">
        <img v-if="guess.personToGuess.image?.type === 'blob'"
          :src="guess.personToGuess.image.url"
          class="object-contain h-[100px]" />
        <span class="text-xs opacity-75">{{ guess.personToGuess.displayName }}</span>
      </div>
    </div>
  </main>
</template>

