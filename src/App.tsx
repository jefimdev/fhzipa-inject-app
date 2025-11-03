import { useState } from "react";
import "./App.css";
import { Modal } from "./Modal";
import { AppleID } from "./AppleID";
import { Device } from "./Device";

function App() {
  const [openModal, setOpenModal] = useState<
    "sidestore" | "pairing" | "other" | null
  >(null);

  return (
    <main className="container">
      <h1>iloader</h1>
      <div className="card-dark">
        <AppleID />
      </div>
      <div className="card-dark">
        <Device />
      </div>
      <div className="card-dark buttons-container">
        <h2>Actions</h2>
        <div className="buttons">
          <button onClick={() => setOpenModal("sidestore")}>
            Install SideStore
          </button>
          <button onClick={() => setOpenModal("other")}>Install Other</button>
          <button>Manage Pairing File</button>
          <button>Manage Certificates</button>
          <button>Manage App IDs</button>
        </div>
      </div>
      <Modal
        isOpen={openModal === "sidestore" || openModal === "other"}
        pages={[]}
        close={() => setOpenModal(null)}
      />
    </main>
  );
}

export default App;
