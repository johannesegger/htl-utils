<script setup lang="ts">
import { onBeforeUnmount, onMounted, useTemplateRef, watch } from 'vue'
import { Compartment, EditorState, type Extension } from '@codemirror/state'
import {
  crosshairCursor,
  drawSelection,
  dropCursor,
  EditorView,
  highlightSpecialChars,
  keymap,
  lineNumbers,
  placeholder as placeholderText,
  rectangularSelection,
} from '@codemirror/view'
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands'
import {
  bracketMatching,
  defaultHighlightStyle,
  foldGutter,
  foldKeymap,
  indentOnInput,
  indentUnit,
  StreamLanguage,
  syntaxHighlighting,
} from '@codemirror/language'
import { autocompletion, closeBrackets, closeBracketsKeymap, completionKeymap } from '@codemirror/autocomplete'
import { highlightSelectionMatches, searchKeymap } from '@codemirror/search'
import { linter, lintKeymap } from '@codemirror/lint'
import { json, jsonParseLinter } from '@codemirror/lang-json'
import { powerShell } from '@codemirror/legacy-modes/mode/powershell'

type Language = 'json' | 'powershell'

const model = defineModel<string>({ required: true })

const props = withDefaults(defineProps<{
  language: Language
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
let view: EditorView | null = null

const languageCompartment = new Compartment()

const jsonLinter = jsonParseLinter()

function languageExtension(language: Language): Extension {
  switch (language) {
    case 'json':
      // Don't complain about an empty document, the user just hasn't typed anything yet.
      return [json(), linter((v) => v.state.doc.length === 0 ? [] : jsonLinter(v))]
    case 'powershell':
      return StreamLanguage.define(powerShell)
  }
}

const theme = EditorView.theme({
  '&': { height: '100%', fontSize: '12px' },
  '&.cm-focused': { outline: 'none' },
  '.cm-scroller': { lineHeight: `${lineHeight}px` },
  // The gutters align themselves to the content lines, so they must not get their own padding.
  '.cm-content': { paddingTop: `${editorPadding}px`, paddingBottom: `${editorPadding}px` },
  '.cm-gutters': { backgroundColor: 'transparent', borderRight: 'none' },
})

onMounted(() => {
  // Set the height on the element instead of binding it, so that a height the user dragged
  // to survives re-renders of the parent.
  frame.value!.style.height = `${frameHeight(props.lines)}px`
  frame.value!.style.minHeight = `${frameHeight(2)}px`

  view = new EditorView({
    parent: host.value!,
    state: EditorState.create({
      doc: model.value,
      extensions: [
        lineNumbers(),
        foldGutter(),
        highlightSpecialChars(),
        history(),
        drawSelection(),
        dropCursor(),
        EditorState.allowMultipleSelections.of(true),
        indentOnInput(),
        syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
        bracketMatching(),
        closeBrackets(),
        autocompletion(),
        rectangularSelection(),
        crosshairCursor(),
        highlightSelectionMatches(),
        keymap.of([
          ...closeBracketsKeymap,
          ...defaultKeymap,
          ...searchKeymap,
          ...historyKeymap,
          ...foldKeymap,
          ...completionKeymap,
          ...lintKeymap,
          indentWithTab,
        ]),
        EditorState.tabSize.of(2),
        indentUnit.of('  '),
        props.placeholder ? placeholderText(props.placeholder) : [],
        languageCompartment.of(languageExtension(props.language)),
        theme,
        EditorView.updateListener.of((update) => {
          if (!update.docChanged) return
          const value = update.state.doc.toString()
          if (value !== model.value) model.value = value
        }),
      ],
    }),
  })
})

watch(model, (value) => {
  if (view && view.state.doc.toString() !== value) {
    view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: value } })
  }
})

watch(() => props.language, (language) => {
  view?.dispatch({ effects: languageCompartment.reconfigure(languageExtension(language)) })
})

onBeforeUnmount(() => {
  view?.destroy()
  view = null
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
