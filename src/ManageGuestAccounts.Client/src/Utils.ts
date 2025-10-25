export const pluralize = (count: number, singularText: string, pluralText: string) => {
    const text = count === 1 ? singularText : pluralText
    return `${count} ${text}`
}