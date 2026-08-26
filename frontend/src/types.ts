export interface ModelCapabilities {
  textInput: boolean;
  imageInput: boolean;
  documentInput: boolean;
  audioInput: boolean;
  textOutput: boolean;
  imageOutput: boolean;
  videoOutput: boolean;
  streaming: boolean;
}

export interface ModelInfo {
  id: string;
  name: string;
  provider: string;
  description: string;
  capabilities: ModelCapabilities;
  modalities: string[];
}

export type Role = "User" | "Assistant" | "System";

export interface ChatMessage {
  id: string;
  role: Role;
  content: string;
  modelIdUsed?: string;
  createdAt: string;
}

export interface ChatSession {
  id: string;
  title: string;
  modelId?: string;
  automaticModel: boolean;
  updatedAt: string;
  messages?: ChatMessage[];
}

/** Derived UI mode used to adapt the layout to a model's primary output. */
export type UiMode = "chat" | "image" | "video";

export function deriveUiMode(caps?: ModelCapabilities): UiMode {
  if (!caps) return "chat";
  if (caps.videoOutput) return "video";
  if (caps.imageOutput && !caps.textOutput) return "image";
  return "chat";
}
