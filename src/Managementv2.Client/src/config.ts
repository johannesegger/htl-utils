import type { WireConfig } from './api'

export type ConfigKind = 'text' | 'file' | 'credential' | 'certificate' | 'sshKey'

export const configKindLabels: Record<ConfigKind, string> = {
  text: 'Text',
  file: 'File',
  credential: 'Credential',
  certificate: 'Protected certificate',
  sshKey: 'SSH key',
}

// A flat entry (rather than a union) so every field is directly v-model-bindable;
// only the fields relevant to `kind` are used when serializing.
export interface ConfigEntry {
  key: string
  kind: ConfigKind
  text: string
  userName: string
  password: string
  fileBase64: string
  fileName: string
  /** A note for whoever edits the config; never passed to an operation. */
  comment: string
}

export function emptyEntry(): ConfigEntry {
  return { key: '', kind: 'text', text: '', userName: '', password: '', fileBase64: '', fileName: '', comment: '' }
}

export function fromDto(config: WireConfig): ConfigEntry[] {
  return Object.entries(config).map(([key, value]) => {
    const entry = { ...emptyEntry(), key }
    entry.comment = value.comment
    if ('userName' in value && 'keyFile' in value) {
      entry.kind = 'sshKey'
      entry.userName = value.userName
      entry.fileBase64 = value.keyFile
    } else if ('userName' in value) {
      entry.kind = 'credential'
      entry.userName = value.userName
      entry.password = value.password
    } else if ('file' in value && 'password' in value) {
      entry.kind = 'certificate'
      entry.fileBase64 = value.file
      entry.password = value.password
    } else if ('file' in value) {
      entry.kind = 'file'
      entry.fileBase64 = value.file
    } else if ('text' in value) {
      entry.kind = 'text'
      entry.text = value.text
    }
    return entry
  })
}

export function toDto(entries: ConfigEntry[]): WireConfig {
  const config: WireConfig = {}
  for (const entry of entries) {
    const comment = entry.comment
    switch (entry.kind) {
      case 'text':
        config[entry.key] = { text: entry.text, comment }
        break
      case 'file':
        config[entry.key] = { file: entry.fileBase64, comment }
        break
      case 'credential':
        config[entry.key] = { userName: entry.userName, password: entry.password, comment }
        break
      case 'certificate':
        config[entry.key] = { file: entry.fileBase64, password: entry.password, comment }
        break
      case 'sshKey':
        config[entry.key] = { userName: entry.userName, keyFile: entry.fileBase64, comment }
        break
    }
  }
  return config
}

export function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const result = reader.result as string
      resolve(result.slice(result.indexOf(',') + 1))
    }
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}
