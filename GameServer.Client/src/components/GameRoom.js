// ... existing imports ...
import SpectatorList from './SpectatorList';

// ... existing code ...

const GameRoom = ({ connection, roomId, onLeaveRoom }) => {
  const [room, setRoom] = useState(null);
  const [players, setPlayers] = useState([]);
  const [spectators, setSpectators] = useState([]);
  const [gameState, setGameState] = useState(null);
  const [currentTurn, setCurrentTurn] = useState(null);
  const [gameResult, setGameResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // ... existing useEffect and other code ...

  useEffect(() => {
    if (!connection) return;

    // ... existing event handlers ...

    // Add handlers for spectator events
    connection.on("SpectatorJoined", (spectatorId, spectatorName) => {
      message.info(`${spectatorName} is now spectating the game.`);
      // Refresh spectator list
      connection.invoke("GetRoomSpectators", roomId)
        .then(spectators => setSpectators(spectators))
        .catch(err => console.error("Error getting spectators:", err));
    });

    connection.on("SpectatorLeft", (spectatorId) => {
      // Refresh spectator list
      connection.invoke("GetRoomSpectators", roomId)
        .then(spectators => setSpectators(spectators))
        .catch(err => console.error("Error getting spectators:", err));
    });

    // ... existing cleanup code ...
  }, [connection, roomId]);

  // ... existing render code ...

  return (
    <div className="game-room">
      {/* ... existing UI ... */}
      
      <Row gutter={16}>
        <Col span={18}>
          {/* Game board */}
          {renderGameBoard()}
        </Col>
        <Col span={6}>
          {/* Player list */}
          <PlayerList 
            players={players} 
            currentTurn={currentTurn} 
            gameState={room?.state} 
          />
          
          {/* Spectator list */}
          <SpectatorList spectators={spectators} />
          
          {/* Game controls */}
          <div className="game-controls">
            {/* ... existing controls ... */}
          </div>
        </Col>
      </Row>
    </div>
  );
};

// ... rest of the file ...