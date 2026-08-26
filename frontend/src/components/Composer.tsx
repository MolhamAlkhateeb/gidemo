import { useRef, useState } from "react";
import { api } from "../api";
import { useSpeech } from "../hooks/useSpeech";
import type { ModelInfo, UiMode } from "../types";

interface Attachment {
  fileId: string;
  fileName: string;
}

interface Props {
  model?: ModelInfo;
  uiMode: UiMode;
  disabled: boolean;
  onSend: (prompt: string, attachmentIds: string[]) => void;
}

export function Composer({ model, uiMode, disabled, onSend }: Props) {
  const [text, setText] = useState("");
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { listening, startListening, stopListening } = useSpeech();

  const canUpload =
    model?.capabilities.imageInput || model?.capabilities.documentInput;

  const placeholder =
    uiMode === "image"
      ? "Describe the image to generate…"
      : uiMode === "video"
      ? "Describe the video to generate…"
      : "Send a message…";

  const handleFiles = async (files: FileList | null) => {
    if (!files) return;
    for (const file of Array.from(files)) {
      const presign = await api.presignUpload(file.name, file.type, file.size);
      await api.uploadToS3(presign.uploadUrl, file);
      setAttachments((a) => [...a, { fileId: presign.fileId, fileName: file.name }]);
    }
  };

  const submit = () => {
    if (!text.trim() && attachments.length === 0) return;
    onSend(text, attachments.map((a) => a.fileId));
    setText("");
    setAttachments([]);
  };

  return (
    <div className="border-t border-base-300 p-3 bg-base-100">
      {attachments.length > 0 && (
        <div className="flex flex-wrap gap-2 mb-2">
          {attachments.map((a) => (
            <span key={a.fileId} className="badge badge-outline gap-1">
              📎 {a.fileName}
            </span>
          ))}
        </div>
      )}
      <div className="flex items-end gap-2">
        {canUpload && (
          <>
            <button
              className="btn btn-ghost btn-circle"
              onClick={() => fileInputRef.current?.click()}
              title="Attach file"
            >
              📎
            </button>
            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              multiple
              accept=".docx,.xlsx,image/*"
              onChange={(e) => handleFiles(e.target.files)}
            />
          </>
        )}

        {model?.capabilities.audioInput !== false && (
          <button
            className={`btn btn-circle ${listening ? "btn-error" : "btn-ghost"}`}
            onClick={() =>
              listening ? stopListening() : startListening((t) => setText((p) => p + t))
            }
            title="Voice input"
          >
            {listening ? "⏹" : "🎤"}
          </button>
        )}

        <textarea
          className="textarea textarea-bordered flex-1 resize-none"
          rows={1}
          placeholder={placeholder}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              submit();
            }
          }}
        />

        <button className="btn btn-primary" disabled={disabled} onClick={submit}>
          Send
        </button>
      </div>
    </div>
  );
}
