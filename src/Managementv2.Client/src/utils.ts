export function pluralize(count: number, singularText: string, pluralText: string) {
    return count === 1 ? `${count} ${singularText}` : `${count} ${pluralText}`
}