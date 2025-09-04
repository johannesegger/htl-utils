export const store = (key: string, value: string | undefined) => {
  if (value === undefined) {
    localStorage.removeItem(key)
  }
  else {
    localStorage.setItem(key, value)
  }
}

export const load = (key: string) => {
  return localStorage.getItem(key) || undefined
}
