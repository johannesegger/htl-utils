export const pluralize = (count: number, singularText: string, pluralText: string) => {
    const text = count === 1 ? singularText : pluralText
    return `${count} ${text}`
}

// see https://developer.mozilla.org/en-US/docs/Web/API/Window/btoa
export const bytesToBase64 = (bytes: Uint8Array) => {
  const binString = Array.from(bytes, byte => String.fromCodePoint(byte)).join('')
  return btoa(binString)
}