import ReactMarkdown from "react-markdown";
import type { Components } from "react-markdown";
import { useSpeech } from "../hooks/useSpeech";
import type { ChatMessage } from "../types";

interface Props {
  messages: ChatMessage[];
  streaming?: string;
}

// Render generated images at a sensible size instead of full resolution.
const markdownComponents: Components = {
  img: ({ node, ...props }) => (
    <img {...props} className="max-w-sm rounded-lg mt-2" loading="lazy" />
  ),
};

export function MessageList({ messages, streaming }: Props) {
  const { speak, speaking, stopSpeaking } = useSpeech();

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4">
      {messages.map((m) => (
        <div
          key={m.id}
          className={`chat ${m.role === "User" ? "chat-end" : "chat-start"}`}
        >
          <div
            className={`chat-bubble ${
              m.role === "User" ? "chat-bubble-primary" : ""
            }`}
          >
            <ReactMarkdown components={markdownComponents}>{m.content}</ReactMarkdown>
          </div>
          {m.role === "Assistant" && (
            <div className="chat-footer opacity-60 flex gap-2 items-center">
              {m.modelIdUsed && <span className="text-xs">{m.modelIdUsed}</span>}
              <button
                className="btn btn-ghost btn-xs"
                onClick={() => (speaking ? stopSpeaking() : speak(m.content))}
                title="Read aloud"
              >
                {speaking ? "⏹" : "🔊"}
              </button>
            </div>
          )}
        </div>
      ))}

      {streaming !== undefined && (
        <div className="chat chat-start">
          <div className="chat-bubble">
            <ReactMarkdown components={markdownComponents}>{streaming || "…"}</ReactMarkdown>
          </div>
        </div>
      )}
    </div>
  );
}
