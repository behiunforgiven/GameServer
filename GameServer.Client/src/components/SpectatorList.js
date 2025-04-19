import React from 'react';
import { List, Avatar, Typography, Badge } from 'antd';
import { EyeOutlined } from '@ant-design/icons';

const { Title } = Typography;

const SpectatorList = ({ spectators }) => {
  if (!spectators || spectators.length === 0) {
    return (
      <div className="spectator-list">
        <Title level={4}>
          <EyeOutlined /> Spectators (0)
        </Title>
        <p>No one is watching this game.</p>
      </div>
    );
  }

  return (
    <div className="spectator-list">
      <Title level={4}>
        <EyeOutlined /> Spectators ({spectators.length})
      </Title>
      <List
        itemLayout="horizontal"
        dataSource={spectators}
        renderItem={spectator => (
          <List.Item>
            <List.Item.Meta
              avatar={
                <Badge status="default" dot>
                  <Avatar src={spectator.avatarUrl || `https://joeschmoe.io/api/v1/${spectator.username}`} />
                </Badge>
              }
              title={spectator.username}
            />
          </List.Item>
        )}
      />
    </div>
  );
};

export default SpectatorList;