import type { WireConfig } from './api'

export type ConfigKind = 'text' | 'file' | 'credential' | 'certificate'

export const configKindLabels: Record<ConfigKind, string> = {
  text: 'Text',
  file: 'File',
  credential: 'Credential',
  certificate: 'Protected certificate',
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
}

export function emptyEntry(): ConfigEntry {
  return { key: '', kind: 'text', text: '', userName: '', password: '', fileBase64: '', fileName: '' }
}

export function fromWire(config: WireConfig): ConfigEntry[] {
  return Object.entries(config).map(([key, value]) => {
    const entry = { ...emptyEntry(), key }
    if (typeof value === 'string') {
      entry.kind = 'text'
      entry.text = value
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
    }
    return entry
  })
}

export function toWire(entries: ConfigEntry[]): WireConfig {
  const config: WireConfig = {}
  for (const entry of entries) {
    switch (entry.kind) {
      case 'text':
        config[entry.key] = entry.text
        break
      case 'file':
        config[entry.key] = { file: entry.fileBase64 }
        break
      case 'credential':
        config[entry.key] = { userName: entry.userName, password: entry.password }
        break
      case 'certificate':
        config[entry.key] = { file: entry.fileBase64, password: entry.password }
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
