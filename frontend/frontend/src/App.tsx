import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import AdminDocuments from "./components/AdminDocuments";

import {
  createChat,
  deleteChat,
  getChats,
  getMessages,
  sendMessage,
} from "./services/chatService";
import type { Chat, Message } from "./services/chatService";
import "github-markdown-css/github-markdown-dark.css";
import "./App.css";

function App() {
  const [chats, setChats] = useState<Chat[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [selectedChatId, setSelectedChatId] = useState<number | null>(null);
  const [message, setMessage] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [view, setView] = useState<"chat" | "admin">("chat");
  const msgInputRef = useRef<HTMLInputElement>(null);

  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const loadChats = async () => {
      try {
        const data = await getChats();

        setChats(data);

        if (data.length > 0) {
          setSelectedChatId(data[0].id);

          const messagesData = await getMessages(data[0].id);
          setMessages(messagesData);
        }
      } catch {
        setError("Kunde inte ladda chattar.");
      }
    };

    loadChats();
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({
      behavior: "smooth",
    });
  }, [messages]);

  useEffect(() => {
    if (!isLoading && selectedChatId && view === "chat") {
      msgInputRef.current?.focus();
    }
  }, [isLoading, selectedChatId, view]);

  const selectChat = async (chatId: number) => {
    try {
      setSelectedChatId(chatId);
      setError("");

      const data = await getMessages(chatId);
      setMessages(data);
    } catch {
      setError("Kunde inte ladda meddelanden.");
    }
  };

  const handleNewChat = async () => {
    try {
      setError("");

      const chat = await createChat();

      setChats((currentChats) => [chat, ...currentChats]);
      setSelectedChatId(chat.id);
      setMessages([]);
      setMessage("");
    } catch {
      setError("Kunde inte skapa ny chatt.");
    }
  };

  const handleDeleteChat = async (chatId: number) => {
    try {
      await deleteChat(chatId);

      const remainingChats = chats.filter(
        (chat) => chat.id !== chatId
      );

      setChats(remainingChats);

      if (selectedChatId === chatId) {
        if (remainingChats.length > 0) {
          setSelectedChatId(remainingChats[0].id);

          const data = await getMessages(remainingChats[0].id);
          setMessages(data);
        } else {
          setSelectedChatId(null);
          setMessages([]);
        }
      }
    } catch {
      setError("Kunde inte radera chatten.");
    }
  };

  const handleSendMessage = async () => {
    if (!selectedChatId || !message.trim() || isLoading) {
      return;
    }

    const userMessage = message;

    try {
        setMessage("");
      setIsLoading(true);
      setError("");

  const userContent = message.trim();

const userMessage = {
    id: Date.now(),
    chatId: selectedChatId,
    role: "user",
    content: userContent,
    createdAt: new Date().toISOString(),
};

const assistantMessage = {
    id: Date.now() + 1,
    chatId: selectedChatId,
    role: "assistant",
    content: "",
    createdAt: new Date().toISOString(),
};

setMessages((currentMessages) => [
    ...currentMessages,
    userMessage,
    assistantMessage,
]);

await sendMessage(
    selectedChatId,
    userContent,
    (chunk) => {
      console.log("CHUNK:", chunk);
        setMessages((currentMessages) =>
            currentMessages.map((msg) =>
                msg.id === assistantMessage.id
                    ? {
                        ...msg,
                        content: msg.content + chunk,
                    }
                    : msg
            )
        );
    }
);



    } catch {
       setMessage(userMessage);
      setError("Något gick fel när meddelandet skickades.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyDown = (
    event: React.KeyboardEvent<HTMLInputElement>
  ) => {
    if (event.key === "Enter") {
      event.preventDefault();
      handleSendMessage();
    }
  };

  return (
     <div className="app">

    <div className="top-navigation">
      <button onClick={() => setView("chat")}>
        Chat
      </button>

      <button onClick={() => setView("admin")}>
        Admin
      </button>
    </div>

    {view === "admin" ? (
      <AdminDocuments />
    ) : (
      <>
        <aside className="sidebar">
        <div className="sidebar-header">
          <div>
            <h1>Personalhandboken</h1>
            <p>Powered by Kalle Anka AI™</p>
          </div>

          <button
            className="new-chat-button"
            onClick={handleNewChat}
          >
            + Ny chatt
          </button>
        </div>

        <div className="chat-list">
          {chats.map((chat) => (
            <div
              key={chat.id}
              className={`chat-item ${
                selectedChatId === chat.id ? "active" : ""
              }`}
            >
              <button
                className="chat-select-button"
                onClick={() => selectChat(chat.id)}
              >
                {chat.title}
              </button>

              <button
                className="delete-button"
                onClick={() => handleDeleteChat(chat.id)}
              >
                🗑
              </button>
            </div>
          ))}
        </div>
      </aside>

      <main className="chat-area">
        <header className="chat-header">
          <h2>
            {selectedChatId
              ? chats.find(
                  (chat) => chat.id === selectedChatId
                )?.title ?? "AI Chat"
              : "Ingen chatt vald"}
          </h2>
        </header>

        <div className="messages">
          {messages.length === 0 && !selectedChatId && (
            <div className="welcome">
              <h2>Välkommen </h2>
              <p>
                Skapa en ny chatt och börja prata med AI:n.
              </p>
            </div>
          )}

          {messages.length === 0 && selectedChatId && (
            <div className="welcome">
              <h2>Ny chatt </h2>
              <p>Skriv något för att börja konversationen.</p>
            </div>
          )}

          {messages.map((item) => (
            <div
              key={item.id}
              className={`message-row ${
                item.role === "user"
                  ? "user-row"
                  : "assistant-row"
              }`}
            >
              <div
                className={`message-bubble ${
                  item.role === "user"
                    ? "user-message"
                    : "assistant-message markdown-body"
                }`}
              >
                {item.role === "assistant" ? (
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                    {item.content}
                  </ReactMarkdown>
                ) : (
                  item.content
                )}
              </div>
            </div>
          ))}

          {isLoading && (
            <div className="message-row assistant-row">
              <div className="message-bubble assistant-message loading">
                AI tänker...
              </div>
            </div>
          )}

          {error && <div className="error">{error}</div>}

          <div ref={messagesEndRef} />
        </div>

        <div className="input-area">
          <input
          ref={msgInputRef}
            type="text"
            value={message}
            onChange={(event) => setMessage(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={
              selectedChatId
                ? "Skriv ett meddelande..."
                : "Välj eller skapa en chatt..."
            }
            disabled={!selectedChatId || isLoading}
          />

          <button
            className="send-button"
            onClick={handleSendMessage}
            disabled={
              !selectedChatId ||
              !message.trim() ||
              isLoading
            }
          >
            Skicka
          </button>
        </div>
      </main>
      </>
    )}
    </div>
  );
}


export default App;
