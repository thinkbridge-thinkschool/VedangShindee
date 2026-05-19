import http from "node:http";
import pino from "pino";
import { db } from "./db.ts";
import { sendJson } from "./http.ts";

type Quote = {
  id: number;
  author: string;
  text: string;
};

const logger = pino();

let shuttingDown = false;
let inFlight = 0;

const server = http.createServer(async (req, res) => {
  inFlight++;

  res.on("finish", () => {
    inFlight--;
  });

  logger.info({
    method: req.method,
    url: req.url
  });

  if (shuttingDown) {
    sendJson(res, 503, {
      error: "Server shutting down"
    });

    return;
  }

  req.on("aborted", () => {
    logger.warn("Request aborted");
  });

  try {
    // GET /quotes
    if (req.method === "GET" && req.url === "/quotes") {
      const rows = db
        .prepare("SELECT * FROM quotes")
        .all() as Quote[];

      sendJson(res, 200, rows);
      return;
    }

    // GET /quotes/:id
    if (
      req.method === "GET" &&
      req.url?.startsWith("/quotes/")
    ) {
      const id = Number(req.url.split("/")[2]);

      const row = db
        .prepare("SELECT * FROM quotes WHERE id = ?")
        .get(id) as Quote | undefined;

      if (!row) {
        sendJson(res, 404, {
          error: "Not found"
        });

        return;
      }

      sendJson(res, 200, row);
      return;
    }

    // POST /quotes
    if (req.method === "POST" && req.url === "/quotes") {
      let body = "";

      for await (const chunk of req) {
        body += chunk;
      }

      const data = JSON.parse(body) as {
        author?: string;
        text?: string;
      };

      if (!data.author || !data.text) {
        sendJson(res, 400, {
          error: "author and text required"
        });

        return;
      }

      const result = db
        .prepare(
          "INSERT INTO quotes (author, text) VALUES (?, ?)"
        )
        .run(data.author, data.text);

      sendJson(res, 201, {
        id: result.lastInsertRowid,
        author: data.author,
        text: data.text
      });

      return;
    }

    // DELETE /quotes/:id
    if (
      req.method === "DELETE" &&
      req.url?.startsWith("/quotes/")
    ) {
      const id = Number(req.url.split("/")[2]);

      const result = db
        .prepare("DELETE FROM quotes WHERE id = ?")
        .run(id);

      if (result.changes === 0) {
        sendJson(res, 404, {
          error: "Not found"
        });

        return;
      }

      sendJson(res, 200, {
        message: "Deleted"
      });

      return;
    }

    sendJson(res, 404, {
      error: "Route not found"
    });
  } catch (error) {
    logger.error(error);

    sendJson(res, 500, {
      error: "Internal server error"
    });
  }
});

server.listen(3000, () => {
  logger.info(
    "Server running on http://localhost:3000"
  );
});

// graceful shutdown
process.on("SIGINT", () => {
  logger.info("SIGINT received");

  shuttingDown = true;

  server.close(() => {
    logger.info("HTTP server closed");

    db.close();

    logger.info("DB closed");

    process.exit(0);
  });

  const interval = setInterval(() => {
    if (inFlight === 0) {
      clearInterval(interval);

      db.close();

      process.exit(0);
    }
  }, 100);
});