<script setup lang="ts">
import { ref } from 'vue'
import ConfigManager from '@/components/ConfigManager.vue'
import OperationsManager from '@/components/OperationsManager.vue'
import CalculationsManager from '@/components/CalculationsManager.vue'

const tabs = [
  { id: 'calculations', label: 'Calculations' },
  { id: 'operations', label: 'Custom operations' },
  { id: 'config', label: 'Configuration' },
] as const

const active = ref<(typeof tabs)[number]['id']>(tabs[0].id)
</script>

<template>
  <div class="bg-pink-700 text-white">
    <div class="mx-auto max-w-5xl p-6">
      <h1 class="text-3xl font-bold small-caps">HTL IT Management</h1>
    </div>
  </div>
  <div class="mx-auto max-w-5xl p-6">
    <nav class="mb-6 flex gap-2 border-b border-gray-200">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        class="-mb-px border-b-2 px-3 py-2 text-sm font-medium cursor-pointer"
        :class="
          active === tab.id
            ? 'border-blue-600 text-blue-700'
            : 'border-transparent text-gray-500 hover:text-gray-800'
        "
        @click="active = tab.id">{{ tab.label }}
      </button>
    </nav>

    <CalculationsManager v-show="active === 'calculations'" />
    <OperationsManager v-show="active === 'operations'" />
    <ConfigManager v-show="active === 'config'" />
  </div>
</template>
