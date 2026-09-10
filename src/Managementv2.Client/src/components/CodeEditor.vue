<script setup lang="ts">
import { onBeforeUnmount, onMounted, shallowRef, useTemplateRef, watch } from 'vue'
import * as monaco from 'monaco-editor/editor.js'
import 'monaco-editor/features/register.all.js'
import 'monaco-editor/languages/definitions/powershell/register.js'
import 'monaco-editor/languages/features/json/register.js'

const model = defineModel<string>({ required: true })

const props = withDefaults(defineProps<{
  language: 'json' | 'powershell'
  /** Initial height of the editor, in text lines. The user can resize it afterwards. */
  lines?: number
  placeholder?: string
}>(), { lines: 12, placeholder: undefined })

const lineHeight = 18
const editorPadding = 4
/** Free strip at the bottom of the frame so the browser's resize grip doesn't sit on top of the editor. */
const gripHeight = 12
const borderWidth = 1

function frameHeight(lines: number) {
  return lines * lineHeight + 2 * editorPadding + gripHeight + 2 * borderWidth
}

const frame = useTemplateRef<HTMLElement>('frame')
const host = useTemplateRef<HTMLElement>('host')
const editor = shallowRef<monaco.editor.IStandaloneCodeEditor | null>(null)

onMounted(() => {
  // Set the height on the element instead of binding it, so that a height the user dragged
  // to survives re-renders of the parent.
  frame.value!.style.height = `${frameHeight(props.lines)}px`
  frame.value!.style.minHeight = `${frameHeight(2)}px`

  const instance = monaco.editor.create(host.value!, {
    value: model.value,
    language: props.language,
    // Relayouts the editor when the user drags the resize grip, or the surrounding layout changes.
    automaticLayout: true,
    placeholder: props.placeholder,
    fontSize: 12,
    lineHeight: lineHeight,
    padding: { top: editorPadding, bottom: editorPadding },
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    // Let the page keep scrolling once the editor is at its top/bottom.
    scrollbar: { alwaysConsumeMouseWheel: false },
    // Suggestions and hovers may exceed the editor bounds.
    fixedOverflowWidgets: true,
    tabSize: 2,
    renderLineHighlight: 'none',
  })
  editor.value = instance

  instance.onDidChangeModelContent(() => {
    const value = instance.getValue()
    if (value !== model.value) model.value = value
  })
})

watch(model, (value) => {
  const instance = editor.value
  if (instance && instance.getValue() !== value) instance.setValue(value)
})

watch(() => props.language, (language) => {
  const editorModel = editor.value?.getModel()
  if (editorModel) monaco.editor.setModelLanguage(editorModel, language)
})

onBeforeUnmount(() => {
  editor.value?.getModel()?.dispose()
  editor.value?.dispose()
  editor.value = null
})
</script>

<template>
  <div
    ref="frame"
    class="w-full resize-y overflow-hidden rounded border border-gray-300 pb-3 focus-within:border-blue-500"
  >
    <div ref="host" class="h-full"></div>
  </div>
</template>
