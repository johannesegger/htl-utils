<script lang="ts" setup>
import PersonForm from './admin/PersonForm.vue'
import SettingsForm from './admin/SettingsForm.vue'
</script>

<template>
  <header class="flex flex-cols items-center p-8 bg-red-200">
    <div class="justify-self-start flex items-center gap-2">
      <img src="/logo.svg" width="32px" height="32px" />
      <h1 class="text-3xl small-caps">Know Name - Administration</h1>
    </div>
  </header>
  <main class="flex flex-col gap-8 p-4">
    <SettingsForm />
    <hr class="opacity-50" />
    <PersonForm />
  </main>
</template>