import type { ModelInfo } from "../types";

const CAP_BADGES: { key: keyof ModelInfo["capabilities"]; label: string }[] = [
  { key: "textInput", label: "Text in" },
  { key: "imageInput", label: "Image in" },
  { key: "documentInput", label: "Docs in" },
  { key: "audioInput", label: "Audio in" },
  { key: "textOutput", label: "Text out" },
  { key: "imageOutput", label: "Image out" },
  { key: "videoOutput", label: "Video out" },
  { key: "streaming", label: "Streaming" },
];

export function ModelInfoCard({ model }: { model: ModelInfo }) {
  return (
    <div className="card bg-base-200 shadow-sm">
      <div className="card-body p-4">
        <h3 className="card-title text-sm">{model.name}</h3>
        <p className="text-xs opacity-70">{model.description}</p>
        <div className="flex flex-wrap gap-1 mt-2">
          {CAP_BADGES.filter((c) => model.capabilities[c.key]).map((c) => (
            <span key={c.label} className="badge badge-primary badge-sm">
              {c.label}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
