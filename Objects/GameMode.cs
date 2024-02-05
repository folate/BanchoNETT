﻿namespace BanchoNET.Objects;

public enum GameMode
{
	VanillaStd = 0,
	VanillaTaiko = 1,
	VanillaCatch = 2,
	VanillaMania = 3,
	
	RelaxStd = 4,
	RelaxTaiko = 5,
	RelaxCatch = 6,
	RelaxMania = 7,
	
	AutopilotStd = 8,
	AutopilotTaiko = 9,
	AutopilotCatch = 10,
	AutopilotMania = 11,
}

public static class GameModeExtensions
{
	public static GameMode AsVanilla(this GameMode gameMode) => (GameMode)((int)gameMode % 4);
}