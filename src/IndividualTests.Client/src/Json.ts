export const replacer = function(this: { [key]: unknown }, key: string, value: unknown) {
  if (this[key] instanceof Date) {
    const d = this[key]
    return new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate(), d.getHours(), d.getMinutes(), d.getSeconds())).toISOString()
  }
  return value
}