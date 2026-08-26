import type { ModelInfo } from "../types";

interface Props {
  models: ModelInfo[];
  selectedId: string;
  onSelect: (id: string) => void;
}

export function ModelPicker({ models, selectedId, onSelect }: Props) {
  return (
    <select
      className="select select-bordered w-full max-w-xs bg-base-100 text-base-content"
      value={selectedId}
      onChange={(e) => onSelect(e.target.value)}
    >
      {models.map((m) => (
        <option key={m.id} value={m.id} className="bg-base-100 text-base-content">
          {m.name} {m.id !== "automatic" ? `— ${m.provider}` : ""}
        </option>
      ))}
    </select>
  );
}
