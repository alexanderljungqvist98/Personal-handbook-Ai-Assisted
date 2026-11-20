import { useState, useEffect } from "react";

interface UploadedDocument {
    fileName: string;
    url: string;
}

function AdminDocuments() {

    const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
    const [isUploading, setIsUploading] = useState(false);
    const [uploadError, setUploadError] = useState("");

    const [documents, setDocuments] = useState<UploadedDocument[]>([]);

    const fetchDocuments = async () => {
        const response = await fetch("http://localhost:5068/api/documents");

        if (!response.ok) {
            console.error("Kunde inte hämta dokumenten");
            return;
        }

        const data = await response.json();
        setDocuments(data);
    };

    useEffect(() => {
        const loadDocuments = async () => {
            const response = await fetch("http://localhost:5068/api/documents");

            const data = await response.json();

            setDocuments(data);
        };

        loadDocuments();
    }, []);

    const uploadDocument = async () => {
        if (selectedFiles.length === 0 || isUploading) return;

        setIsUploading(true);
        setUploadError("");
        const formData = new FormData();
        selectedFiles.forEach((file) => formData.append("files", file));

        try {
            const response = await fetch("http://localhost:5068/api/documents/", {
                method: "POST",
                body: formData
            });

            if (!response.ok) throw new Error("Upload failed");

            setSelectedFiles([]);
            await fetchDocuments();
        } catch {
            setUploadError("Uppladdningen eller indexeringen misslyckades. Vissa filer kan redan ha laddats upp. Försök igen.");
        } finally {
            setIsUploading(false);
        }
    };
    

    const deleteDocument = async (fileName: string) => {
        const response = await fetch(`http://localhost:5068/api/documents/${fileName}`, {
            method: "DELETE"
        });

        if (!response.ok) {
            console.error("Kunde inte ta bort dokumentet");
            return;
        }

        await fetchDocuments();
    };

    return (
        <main className="admin-page">
            <header className="admin-header">
                <div>
                    <h1>Dokumentadministration</h1>
          <p>Hantera företagets interna dokument</p>
        </div>
      </header>

      <section className="upload-card">
  <h2>Ladda upp dokument</h2>

  <div className="upload-zone">
    <div className="upload-icon"></div>

    <h3>Välj dokument</h3>

    <p>
      Välj en eller flera filer från datorn
    </p>

    <label className="file-select-button">
      Välj filer

      <input
        type="file"
        multiple
        disabled={isUploading}
        accept=".pdf,.docx,.md,.txt"
        onChange={(event) => {
          setSelectedFiles(Array.from(event.target.files ?? []));
          setUploadError("");
          event.target.value = "";
        }}
        hidden
      />
    </label>

    {selectedFiles.map((file) => (
      <div className="selected-file" key={file.name}>
         {file.name}
      </div>
    ))}

        <button className="upload-button" onClick={uploadDocument} disabled={isUploading || selectedFiles.length === 0}>
          {isUploading ? "Laddar upp..." : "Ladda upp"}
        </button>

    {uploadError && <p role="alert">{uploadError}</p>}

    <small>
      PDF • Word • Markdown • TXT
    </small>
  </div>
</section>

      <section className="documents-section">
        <h2>Uppladdade dokument</h2>

        <div className="document-list">
        {
            documents.map((document) => (
                <div className= "document-card" key = { document.fileName } >
                <div>
                <h3>{ document.fileName } </h3>
                </div>

                < div className = "document-actions" >

            < button className = "delete-document-button" onClick={() => deleteDocument(document.fileName)} >
            Ta bort
            </button>
            </div>
            </div>
            ))
        }
        </div>
      </section>
    </main>
  );
}

export default AdminDocuments;
