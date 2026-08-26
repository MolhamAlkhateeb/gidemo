import type { ModelCapabilities } from "../types";

export type CapabilityKey = keyof ModelCapabilities;

const OPTIONS: { key: CapabilityKey; label: string }[] = [
  { key: "textOutput", label: "Text generation" },
  { key: "imageOutput", label: "Image generation" },
  { key: "videoOutput", label: "Video generation" },
  { key: "imageInput", label: "Image understanding" },
  { key: "documentInput", label: "Document input" },
  { key: "audioInput", label: "Audio input" },
  { key: "streaming", label: "Streaming" },
];

interface Props {
  active: Set<CapabilityKey>;
  onToggle: (key: CapabilityKey) => void;
  onClear: () => void;
}

export function CapabilityFilter({ active, onToggle, onClear }: Props) {
  return (
    <div className="dropdown">
      <div tabIndex={0} role="button" className="btn btn-sm">
        Filter{active.size ? ` (${active.size})` : ""} ▾
      </div>
      <ul
        tabIndex={0}
        className="dropdown-content menu bg-base-100 text-base-content rounded-box z-10 w-60 p-2 shadow"
      >
        {OPTIONS.map((o) => (
          <li key={o.key}>
            <label className="label cursor-pointer justify-start gap-2">
              <input
                type="checkbox"
                className="checkbox checkbox-sm"
                checked={active.has(o.key)}
                onChange={() => onToggle(o.key)}
              />
              <span className="label-text">{o.label}</span>
            </label>
          </li>
        ))}
        {active.size > 0 && (
          <li>
            <button className="btn btn-ghost btn-xs" onClick={onClear}>
              Clear filters
            </button>
          </li>
        )}
      </ul>
    </div>
  );
}
