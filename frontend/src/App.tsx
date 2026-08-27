import { useEffect, useMemo, useState } from "react";
import { api, streamChat } from "./api";
import { loadConfig, getToken, logout, type AppConfig } from "./auth";
import { CapabilityFilter, type CapabilityKey } from "./components/CapabilityFilter";
import { Composer } from "./components/Composer";
import { Login } from "./components/Login";
import { MessageList } from "./components/MessageList";
import { ModelInfoCard } from "./components/ModelInfoCard";
import { ModelPicker } from "./components/ModelPicker";
import { Sidebar } from "./components/Sidebar";
import { deriveUiMode, type ChatMessage, type ChatSession, type ModelInfo } from "./types";

export default function App() {
  const [config, setConfig] = useState<AppConfig | null>(null);
  const [authed, setAuthed] = useState(false);
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [selectedModelId, setSelectedModelId] = useState("automatic");
  const [capFilters, setCapFilters] = useState<Set<CapabilityKey>>(new Set());
  const [sessions, setSessions] = useState<ChatSession[]>([]);
  const [activeSessionId, setActiveSessionId] = useState<string | undefined>();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [streaming, setStreaming] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);

  const selectedModel = useMemo(
    () => models.find((m) => m.id === selectedModelId),
    [models, selectedModelId]
  );
  const uiMode = deriveUiMode(selectedModel?.capabilities);

  // Models matching every active capability filter (Automatic always qualifies).
  const filteredModels = useMemo(
    () =>
      models.filter((m) =>
        [...capFilters].every((k) => m.capabilities[k])
      ),
    [models, capFilters]
  );

  const toggleFilter = (key: CapabilityKey) =>
    setCapFilters((prev) => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });

  // Keep the selection valid when filtering hides the current model.
  useEffect(() => {
    if (filteredModels.length && !filteredModels.some((m) => m.id === selectedModelId)) {
      setSelectedModelId(filteredModels[0].id);
    }
  }, [filteredModels, selectedModelId]);

  // Resolve auth state on load: skip login when auth is disabled or a token exists.
  useEffect(() => {
    loadConfig()
      .then((c) => {
        setConfig(c);
        if (!c.authEnabled || getToken()) setAuthed(true);
      })
      .catch(console.error);
  }, []);

  useEffect(() => {
    if (!authed) return;
    api.listModels().then(setModels).catch(console.error);
    refreshSessions();
  }, [authed]);

  const refreshSessions = () =>
    api.listSessions().then(setSessions).catch(console.error);

  const openSession = async (id: string) => {
    const s = await api.getSession(id);
    setActiveSessionId(id);
    setMessages(s.messages ?? []);
    if (s.modelId) setSelectedModelId(s.modelId);
  };

  const newChat = () => {
    setActiveSessionId(undefined);
    setMessages([]);
  };

  const deleteSession = async (id: string) => {
    await api.deleteSession(id);
    if (id === activeSessionId) newChat();
    refreshSessions();
  };

  const send = async (prompt: string, attachmentIds: string[]) => {
    setBusy(true);
    setStreaming("");
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: "User",
      content: prompt,
      createdAt: new Date().toISOString(),
    };
    setMessages((m) => [...m, userMsg]);

    let acc = "";
    let usedModel = selectedModelId;

    await streamChat(
      { sessionId: activeSessionId, modelId: selectedModelId, prompt, attachmentIds },
      {
        onSession: (id) => {
          setActiveSessionId(id);
          refreshSessions();
        },
        onRouted: (id) => (usedModel = id),
        onDelta: (t) => {
          acc += t;
          setStreaming(acc);
        },
        onDone: (id) => {
          usedModel = id || usedModel;
          setMessages((m) => [
            ...m,
            {
              id: crypto.randomUUID(),
              role: "Assistant",
              content: acc,
              modelIdUsed: usedModel,
              createdAt: new Date().toISOString(),
            },
          ]);
          setStreaming(undefined);
          setBusy(false);
        },
        onError: (err) => {
          const msg = err instanceof Error ? err.message : "The request failed. Try a different model.";
          setMessages((m) => [
            ...m,
            {
              id: crypto.randomUUID(),
              role: "Assistant",
              content: `⚠️ ${msg}`,
              createdAt: new Date().toISOString(),
            },
          ]);
          setStreaming(undefined);
          setBusy(false);
        },
      }
    );
  };

  const modeBanner =
    uiMode === "image"
      ? "bg-gradient-to-r from-pink-500 to-violet-500 text-white"
      : uiMode === "video"
      ? "bg-gradient-to-r from-amber-500 to-red-500 text-white"
      : "bg-base-200";

  if (!config) {
    return (
      <div className="min-h-full flex items-center justify-center">
        <span className="loading loading-spinner loading-lg" />
      </div>
    );
  }

  if (config.authEnabled && !authed) {
    return <Login config={config} onSuccess={() => setAuthed(true)} />;
  }

  return (
    <div className="flex h-full">
      <Sidebar
        sessions={sessions}
        activeId={activeSessionId}
        onSelect={openSession}
        onNew={newChat}
        onDelete={deleteSession}
      />

      <main className="flex-1 flex flex-col h-full">
        <header className={`p-3 flex items-center gap-4 ${modeBanner}`}>
          <ModelPicker
            models={filteredModels}
            selectedId={selectedModelId}
            onSelect={setSelectedModelId}
          />
          <CapabilityFilter
            active={capFilters}
            onToggle={toggleFilter}
            onClear={() => setCapFilters(new Set())}
          />
          <span className="font-semibold uppercase tracking-wide text-sm">
            {uiMode === "image"
              ? "🎨 Image Studio"
              : uiMode === "video"
              ? "🎬 Video Studio"
              : "💬 Chat"}
          </span>
          {config.authEnabled && (
            <button className="btn btn-ghost btn-sm ml-auto" onClick={logout}>
              Sign out
            </button>
          )}
        </header>

        {selectedModel && selectedModel.id !== "automatic" && (
          <div className="p-3">
            <ModelInfoCard model={selectedModel} />
          </div>
        )}

        <MessageList messages={messages} streaming={streaming} />

        <Composer
          model={selectedModel}
          uiMode={uiMode}
          disabled={busy}
          onSend={send}
        />
      </main>
    </div>
  );
}
