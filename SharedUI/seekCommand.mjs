// One short-lived gesture update; never a queued command or an absolute position.
export function parseSeekCommand(value) {
  if (typeof value !== 'string' || !/^seek:-?(?:[1-9]|[1-5][0-9]|60)$/.test(value)) return null
  return Number(value.slice(5))
}
