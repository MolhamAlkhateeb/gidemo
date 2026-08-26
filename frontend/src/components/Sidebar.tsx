import type { ChatSession } from "../types";

interface Props {
  sessions: ChatSession[];
  activeId?: string;
  onSelect: (id: string) => void;
  onNew: () => void;
  onDelete: (id: string) => void;
}

export function Sidebar({ sessions, activeId, onSelect, onNew, onDelete }: Props) {
  return (
    <aside className="w-64 bg-base-200 flex flex-col h-full">
      <div className="p-3">
        <button className="btn btn-primary btn-block" onClick={onNew}>
          + New chat
        </button>
      </div>
      <ul className="menu flex-1 overflow-y-auto px-2">
        {sessions.map((s) => (
          <li key={s.id}>
            <a
              className={s.id === activeId ? "active" : ""}
              onClick={() => onSelect(s.id)}
            >
              <span className="truncate flex-1">{s.title}</span>
              <button
                className="btn btn-ghost btn-xs"
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete(s.id);
                }}
              >
                ✕
              </button>
            </a>
          </li>
        ))}
      </ul>
    </aside>
  );
}
