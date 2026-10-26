export interface Note {
  id: string;
  userId: string;
  title: string;
  body: string;
  updatedAt: string;
}

// Set VITE_API_URL at build time (the deployed API address). Locally it defaults to the dev API.
const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5080";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

export const notesApi = {
  list: () => request<Note[]>("/api/notes"),
  create: (title: string, body: string) =>
    request<Note>("/api/notes", { method: "POST", body: JSON.stringify({ title, body }) }),
  remove: (id: string) => request<void>(`/api/notes/${id}`, { method: "DELETE" }),
};
