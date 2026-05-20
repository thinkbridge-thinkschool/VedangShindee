import type { IncomingMessage, ServerResponse } from "node:http";
import type Database from "better-sqlite3";
import { HttpError, readJson, sendJson, sendError, matchRoute } from "./http.ts";
import { listTasks, getTask, createTask, updateTask, deleteTask } from "./db.ts";

export async function dispatch(req: IncomingMessage, res: ServerResponse, signal: AbortSignal, db: Database.Database): Promise<void> {
  const method = req.method ?? "GET";
  const pathname = new URL(req.url ?? "/", "http://localhost").pathname.replace(/\/$/, "") || "/";
  const start = Date.now();

  try {
    // GET /tasks
    if (method === "GET" && matchRoute("/tasks", pathname)) {
      sendJson(res, 200, listTasks(db));
      console.log(JSON.stringify({ method, path: pathname, status: 200, ms: Date.now() - start }));
      return;
    }

    // POST /tasks
    if (method === "POST" && matchRoute("/tasks", pathname)) {
      const body = await readJson<Record<string, unknown>>(req, signal);
      const title = body["title"];
      if (typeof title !== "string" || !title.trim()) throw new HttpError(400, '"title" must be a non-empty string');
      const task = createTask(db, title.trim());
      sendJson(res, 201, task);
      console.log(JSON.stringify({ method, path: pathname, status: 201, ms: Date.now() - start }));
      return;
    }

    // PATCH /tasks/:id
    const patchParams = matchRoute("/tasks/:id", pathname);
    if (method === "PATCH" && patchParams) {
      const id = Number(patchParams["id"]);
      if (!Number.isInteger(id) || id < 1) throw new HttpError(400, "Invalid id");
      if (!getTask(db, id)) { sendError(res, 404, `Task ${id} not found`); return; }
      const body = await readJson<Record<string, unknown>>(req, signal);
      const title = "title" in body ? String(body["title"]) : undefined;
      const done = "done" in body ? Boolean(body["done"]) : undefined;
      const updated = updateTask(db, id, title, done);
      sendJson(res, 200, updated);
      console.log(JSON.stringify({ method, path: pathname, status: 200, ms: Date.now() - start }));
      return;
    }

    // DELETE /tasks/:id
    const deleteParams = matchRoute("/tasks/:id", pathname);
    if (method === "DELETE" && deleteParams) {
      const id = Number(deleteParams["id"]);
      if (!Number.isInteger(id) || id < 1) throw new HttpError(400, "Invalid id");
      if (!deleteTask(db, id)) { sendError(res, 404, `Task ${id} not found`); return; }
      res.writeHead(204); res.end();
      console.log(JSON.stringify({ method, path: pathname, status: 204, ms: Date.now() - start }));
      return;
    }

    sendError(res, 404, `Cannot ${method} ${pathname}`);
  } catch (err) {
    if (err instanceof HttpError) {
      if (err.status === 499) return;
      sendError(res, err.status, err.message);
      return;
    }
    console.error(err);
    sendError(res, 500, "Internal server error");
  }
}