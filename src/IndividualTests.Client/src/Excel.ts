type XLSXCell = {
  t: 'b' | 'e' | 'n' | 'd' | 's' | 'z'
  v: Date | string | number | undefined
  w: string
}
export type Cell = {
  type: 'date'
  value: Date
  text: string
} | {
  type: 'string'
  value: string
  text: string
} | {
  type: 'empty'
  value: undefined
  text: ''
}
export namespace Cell {
  export const parse = (cell: XLSXCell | undefined) : Cell => {
    if (cell === undefined) return { type: 'empty', value: undefined, text: '' }
    if (cell.t === 'd') return { type: 'date', value: <Date>cell.v, text: cell.w }
    return { type: 'string', value: cell.w, text: cell.w }
  }
}