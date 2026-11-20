const API_URL = "http://localhost:5068/api";

export interface Chat {
  id: number;
  title: string;
  createdAt: string;
}

export interface Message {
  id: number;
  chatId: number;
  role: string;
  content: string;
  createdAt: string;
}

export async function getChats(): Promise<Chat[]> {
  const response = await fetch(`${API_URL}/chats`);

  if (!response.ok) {
    throw new Error("Kunde inte hämta chattar");
  }

  return await response.json();
}

export async function createChat(): Promise<Chat> {
  const response = await fetch(`${API_URL}/chats`, {
    method: "POST",
  });

  if (!response.ok) {
    throw new Error("Kunde inte skapa chatt");
  }

  return await response.json();
}

export async function deleteChat(chatId: number): Promise<void> {
  const response = await fetch(`${API_URL}/chats/${chatId}`, {
    method: "DELETE",
  });

  if (!response.ok) {
    throw new Error("Kunde inte radera chatt");
  }
}

export async function getMessages(chatId: number): Promise<Message[]> {
  const response = await fetch(`${API_URL}/chats/${chatId}/messages`);

  if (!response.ok) {
    throw new Error("Kunde inte hämta meddelanden");
  }

  return await response.json();
}

export async function sendMessage(
    chatId: number,
    content: string,
    onChunk: (chunk: string) => void
): Promise<string> {

    const response = await fetch(
        `${API_URL}/chats/${chatId}/messages`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ content })
        }
    );

    if (!response.ok) {
        throw new Error("Kunde inte skicka meddelandet");
    }

    if (!response.body) {
        throw new Error("Servern skickade ingen stream");
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();

    let result = "";

    while (true) {
        const { done, value } = await reader.read();

        if (done) {
            break;
        }

       const chunk = decoder.decode(value, { stream: true });

result += chunk;
onChunk(chunk);
    }

    // Säkerställer att eventuell kvarvarande text dekodas
    result += decoder.decode();

    return result;
}