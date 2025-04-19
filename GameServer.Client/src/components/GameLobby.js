// ... existing code ...

const GameLobby = ({ connection, onJoinRoom }) => {
  // ... existing state and functions ...

  const handleSpectateGame = (roomId) => {
    if (!connection) return;

    connection.invoke("JoinAsSpectator", roomId)
      .then(result => {
        if (result) {
          onJoinRoom(roomId);
        } else {
          message.error("Failed to join as spectator.");
        }
      })
      .catch(err => {
        console.error("Error joining as spectator:", err);
        message.error("Error joining as spectator: " + err.message);
      });
  };

  // ... existing render code ...

  const renderRoomList = () => {
    return (
      <List
        itemLayout="horizontal"
        dataSource={rooms}
        renderItem={room => (
          <List.Item
            actions={[
              room.state === "Waiting" && room.players.length < room.maxPlayers ? (
                <Button 
                  type="primary" 
                  onClick={() => handleJoinRoom(room.id, room.isPrivate)}
                >
                  Join
                </Button>
              ) : (
                <Button 
                  type="default" 
                  onClick={() => handleSpectateGame(room.id)}
                >
                  Spectate
                </Button>
              )
            ]}
          >
            <List.Item.Meta
              avatar={<Avatar icon={<TeamOutlined />} />}
              title={`${room.name} (${room.players.length}/${room.maxPlayers})`}
              description={`Game Type: ${room.gameType} | State: ${room.state} | ${room.isPrivate ? "Private" : "Public"}`}
            />
          </List.Item>
        )}
      />
    );
  };

  // ... rest of the file ...
};

// ... rest of the file ...