﻿using BanchoNET.Models;
using BanchoNET.Models.Dtos;
using BanchoNET.Objects.Channels;
using BanchoNET.Objects.Players;
using BanchoNET.Objects.Privileges;
using BanchoNET.Packets;
using BanchoNET.Utils;
using Microsoft.EntityFrameworkCore;
using Channel = BanchoNET.Objects.Channels.Channel;

namespace BanchoNET.Services;

public sealed class BanchoSession
{
	private static readonly Lazy<BanchoSession> Lazy = new(() => new BanchoSession());
	public static BanchoSession Instance => Lazy.Value;

	private readonly DbContextOptions<BanchoDbContext> _dbOptions;
	
	private BanchoSession()
	{
		_dbOptions = new DbContextOptionsBuilder<BanchoDbContext>().UseMySQL("server=127.0.0.1;database=utopia;user=root;password=;").Options;
		BanchoBot = _bots[1];
	}
	
	#region PlayersCollections
	
	private readonly Dictionary<int, Player> _players = [];
	private readonly Dictionary<int, Player> _restricted = [];
	private readonly Dictionary<int, Player> _bots = new()
	{
		{
			1, new Player(new PlayerDto { Id = 1 })
			{
				Username = "Nyzrum Bot", //TODO laod from config/db
				LastActivityTime = DateTime.MaxValue,
				Privileges = Privileges.Staff,
			}
		}
	};
	
	public Player BanchoBot { get; }
	public Dictionary<int,Player>.ValueCollection Players => _players.Values;
	public Dictionary<int,Player>.ValueCollection Restricted => _restricted.Values;
	public Dictionary<int,Player>.ValueCollection Bots => _bots.Values;

	#endregion

	private readonly Dictionary<string, string> _passwordHashes = [];

	#region Channels

	private readonly List<Channel> _spectatorChannels = [];
	private readonly List<Channel> _multiplayerChannels = [];
	private readonly List<Channel> _channels =
	[
		new Channel
		{
			Id = 0,
			Name = "#osu",
			Description = "Main osu! chatroom",
			AutoJoin = true,
			Hidden = false,
			ReadOnly = false,
			Instance = false,
			ReadPrivileges = ClientPrivileges.Player,
			WritePrivileges = ClientPrivileges.Player,
			Players = []
		},
		new Channel
		{
			Id = 1,
			Name = "#lobby",
			Description = "Multiplayer chatroom",
			AutoJoin = false,
			Hidden = false,
			ReadOnly = false,
			Instance = false,
			ReadPrivileges = ClientPrivileges.Player,
			WritePrivileges = ClientPrivileges.Player,
			Players = []
		},
		new Channel
		{
			Id = 2,
			Name = "#staff",
			Description = "osu! staff chatroom",
			AutoJoin = false,
			Hidden = true,
			ReadOnly = false,
			Instance = false,
			ReadPrivileges = ClientPrivileges.Owner,
			WritePrivileges = ClientPrivileges.Owner,
			Players = []
		},
	];

	#endregion

	public void InsertPasswordHash(string passwordHash, string passwordMD5)
	{
		_passwordHashes[passwordHash] = passwordMD5;
	}
	
	public void AppendPlayer(Player player)
	{
		if (player.Restricted) _restricted[player.Id] = player;
		else _players[player.Id] = player;
	}

	public void LogoutPlayer(Player player)
	{
		if (DateTime.UtcNow - player.LoginTime < TimeSpan.FromSeconds(1)) return;
		
		if (player.Lobby != null) player.LeaveMatch();

		player.Spectating?.RemoveSpectator();

		while (player.Channels.Count != 0)
			player.LeaveChannel(player.Channels[0]);

		_players.Remove(player.Id);

		if (!player.Restricted)
		{
			using var logoutPacket = new ServerPackets();
			logoutPacket.Logout(player.Id);
			EnqueueToPlayers(logoutPacket.GetContent());
		}
		
		player.UpdateLatestActivity();
	}
	
	public Player? GetPlayer(int id = 1, string username = "", Guid token = new())
	{
		if (id > 1 && _players.TryGetValue(id, out var value)) 
			return value;

		if (username != "")
		{
			foreach (var player in _players.Where(p => p.Value.Username == username))
				return player.Value;
		}

		if (token != Guid.Empty)
		{
			foreach (var player in _players.Where(p => p.Value.Token == token))
				return player.Value;
		}

		return null;
	}
	
	public bool CheckHashes(string passwordMD5, string passwordHash)
	{
		if (_passwordHashes.TryGetValue(passwordHash, out var md5))
			return md5 == passwordMD5;

		if (!BCrypt.Net.BCrypt.Verify(passwordMD5, passwordHash)) 
			return false;
		
		_passwordHashes[passwordHash] = passwordMD5;
		return true;
	}
	
	public List<Channel> GetAutoJoinChannels(Player player)
	{
		var joinChannels = new List<Channel>();
		//var playerPrivs = player.Privileges;

		foreach (var channel in Instance._channels)
		{
			if (!channel.AutoJoin || 
			    !player.ToBanchoPrivileges().HasPrivilege(channel.ReadPrivileges) ||
			    channel.Name == "#lobby")
			{
				continue;
			}

			joinChannels.Add(channel);
			
			//TODO Send to all players present in the channel to update their player count
		}

		return joinChannels;
	}

	public Channel? GetChannel(string name, ChannelType type = ChannelType.Normal)
	{
		return type switch
		{
			ChannelType.Normal => _channels.FirstOrDefault(c => c.Name == name),
			ChannelType.Spectator => _spectatorChannels.FirstOrDefault(c => c.Name == name),
			ChannelType.Multiplayer => _multiplayerChannels.FirstOrDefault(c => c.Name == name),
			_ => null
		};
	}

	/*public void EnqueuePlayerLogin(ServerPackets loginPackets, Player playerToLogIn)
	{
		using var playerLogin = new ServerPackets();
		playerLogin.UserPresence(playerToLogIn);
		playerLogin.UserStats(playerToLogIn);
		var loginData = playerLogin.GetContent();
		
		foreach (var bot in Bots)
		{
			loginPackets.BotPresence(bot);
			loginPackets.BotStats(bot);
		}
		
		foreach (var player in Players)
		{
			player.Enqueue(loginData);
			loginPackets.UserPresence(player);
			loginPackets.UserStats(player);
		}
		
		foreach (var restrictedPlayer in Restricted)
		{
			restrictedPlayer.Enqueue(loginData);
		}
	}*/

	public void EnqueueToPlayers(byte[] data)
	{
		foreach (var player in _players.Values)
			player.Enqueue(data);

		foreach (var player in _restricted.Values)
			player.Enqueue(data);
	}
}