

export const logger = {
  info: (data: unknown, msg?: string) =>
    console.log(JSON.stringify({ level: "info", msg: msg ?? data, ...(msg ? (data as object) : {}) })),
  warn: (data: unknown, msg?: string) =>
    console.warn(JSON.stringify({ level: "warn", msg: msg ?? data, ...(msg ? (data as object) : {}) })),
  error: (data: unknown, msg?: string) =>
    console.error(JSON.stringify({ level: "error", msg: msg ?? data, ...(msg ? (data as object) : {}) })),
};