/** Notify UI when session storage is cleared due to auth failure. */
type SessionClearedListener = () => void;

const listeners: Set<SessionClearedListener> = new Set();

export function onSessionCleared(listener: SessionClearedListener): () => void {
  listeners.add(listener);
  return (): void => {
    listeners.delete(listener);
  };
}

export function notifySessionCleared(): void {
  for (const listener of listeners) {
    listener();
  }
}
