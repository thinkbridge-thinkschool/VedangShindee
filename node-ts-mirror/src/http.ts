import http from "node:http";

export function sendJson(
  res: http.ServerResponse,
  status: number,
  data: unknown
) {
  res.writeHead(status, {
    "Content-Type": "application/json"
  });

  res.end(JSON.stringify(data));
}