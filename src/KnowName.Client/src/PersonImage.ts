import { uiFetch } from "./UIFetch"

export type PersonImage =
  { type: 'link', url: string } |
  { type: 'blob', url: string } |
  undefined

export namespace PersonImage {
  export const fromLink = (url: string | null) : PersonImage => {
    return url !== null ? { type: 'link', url: url } : undefined
  }
  export const load = async (image: PersonImage, size?: { width?: number, height?: number }) : Promise<PersonImage> => {
    if (image?.type !== 'link') return image
    const url =
      [
        image.url,
        [
          ...(size?.width !== undefined ? [`width=${size.width}`] : []),
          ...(size?.height !== undefined ? [`height=${size.height}`] : []),
        ].join('&')
      ].join('?')
    const response = await uiFetch(url)
    if (response.ok) {
      const image = await response.blob()
      return { type: 'blob', url: URL.createObjectURL(image)}
    }
  }
}