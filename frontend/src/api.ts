import type { ChatSession, ModelInfo } from "./types";

export interface PresignUploadResponse {
  fileId: string;
  uploadUrl: string;
  s3Key: string;
}

const BASE = "/api";

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

export const api = {
  listModels: () => fetch(`${BASE}/models`).then(json<ModelInfo[]>),

  listSessions: () => fetch(`${BASE}/sessions`).then(json<ChatSession[]>),

  getSession: (id: string) =>
    fetch(`${BASE}/sessions/${id}`).then(json<ChatSession>),

  deleteSession: (id: string) =>
    fetch(`${BASE}/sessions/${id}`, { method: "DELETE" }),

  presignUpload: (fileName: string, contentType: string, sizeBytes: number) =>
    fetch(`${BASE}/files/presign`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fileName, contentType, sizeBytes }),
    }).then(json<PresignUploadResponse>),

  uploadToS3: (url: string, file: File) =>
    fetch(url, {
      method: "PUT",
      headers: { "Content-Type": file.type },
      body: file,
    }),

  textToSpeech: (text: string, voiceId?: string) =>
    fetch(`${BASE}/speech/tts`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text, voiceId }),
    }).then((r) => r.blob()),
};

export interface StreamHandlers {
  onSession?: (sessionId: string) => void;
  onRouted?: (modelId: string) => void;
  onDelta: (text: string) => void;
  onDone?: (modelId: string) => void;
  onError?: (err: unknown) => void;
}

/**
 * Posts a chat turn and consumes the Server-Sent Events stream.
 * Uses fetch + ReadableStream because EventSource cannot issue POST bodies.
 */
export async function streamChat(
  body: {
    sessionId?: string;
    modelId: string;
    prompt: string;
    attachmentIds?: string[];
  },
  handlers: StreamHandlers,
  signal?: AbortSignal
): Promise<void> {
  try {
    const res = await fetch(`${BASE}/chat/stream`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal,
    });
    if (!res.body) throw new Error("No response body");

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const events = buffer.split("\n\n");
      buffer = events.pop() ?? "";

      for (const raw of events) {
        const evt = parseEvent(raw);
        if (!evt) continue;
        switch (evt.event) {
          case "session":
            handlers.onSession?.(evt.data.sessionId);
            break;
          case "routed":
            handlers.onRouted?.(evt.data.modelId);
            break;
          case "delta":
            handlers.onDelta(evt.data.text);
            break;
          case "done":
            handlers.onDone?.(evt.data.modelId);
            break;
          case "error":
            handlers.onError?.(new Error(evt.data.message ?? "Model request failed"));
            break;
        }
      }
    }
  } catch (err) {
    handlers.onError?.(err);
  }
}

function parseEvent(raw: string): { event: string; data: any } | null {
  let event = "message";
  let data = "";
  for (const line of raw.split("\n")) {
    if (line.startsWith("event:")) event = line.slice(6).trim();
    else if (line.startsWith("data:")) data += line.slice(5).trim();
  }
  if (!data) return null;
  try {
    return { event, data: JSON.parse(data) };
  } catch {
    return null;
  }
}
