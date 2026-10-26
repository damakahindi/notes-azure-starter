import { useEffect, useState } from "react";
import { useIsAuthenticated } from "@azure/msal-react";
import { Note, notesApi } from "./api";
import { authEnabled, loginRequest, msal } from "./auth";

function SignInBar() {
  const signedIn = useIsAuthenticated();
  return (
    <p>
      {signedIn ? (
        <button onClick={() => msal!.logoutRedirect()}>Sign out</button>
      ) : (
        <button onClick={() => msal!.loginRedirect(loginRequest)}>Sign in</button>
      )}
    </p>
  );
}

export default function App() {
  const [notes, setNotes] = useState<Note[]>([]);
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [error, setError] = useState<string | null>(null);

  const load = () =>
    notesApi.list().then(setNotes).catch((e: Error) => setError(e.message));

  useEffect(() => {
    load();
  }, []);

  const add = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      await notesApi.create(title, body);
      setTitle("");
      setBody("");
      setError(null);
      await load();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const remove = async (id: string) => {
    await notesApi.remove(id);
    await load();
  };

  return (
    <main style={{ maxWidth: 640, margin: "2rem auto", fontFamily: "system-ui" }}>
      <h1>Notes</h1>
      {authEnabled && <SignInBar />}
      <form onSubmit={add} style={{ display: "grid", gap: 8 }}>
        <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Title" required />
        <textarea value={body} onChange={(e) => setBody(e.target.value)} placeholder="Write a note" rows={3} />
        <button type="submit">Add note</button>
      </form>
      {error && <p role="alert" style={{ color: "crimson" }}>Error: {error}</p>}
      <ul style={{ listStyle: "none", padding: 0 }}>
        {notes.map((n) => (
          <li key={n.id} style={{ borderBottom: "1px solid #ddd", padding: "8px 0" }}>
            <strong>{n.title}</strong>
            <p style={{ margin: "4px 0" }}>{n.body}</p>
            <small>{new Date(n.updatedAt).toLocaleString()}</small>{" "}
            <button onClick={() => remove(n.id)}>Delete</button>
          </li>
        ))}
      </ul>
    </main>
  );
}
