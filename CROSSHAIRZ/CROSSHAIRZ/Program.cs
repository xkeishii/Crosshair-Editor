using ClickableTransparentOverlay;
using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CROSSHAIRZ
{
	public class CrosshairSettings
	{
		// Menu & Global
		public Vector4 MenuColor { get; set; } = new Vector4(0.62f, 0.24f, 0.94f, 1.0f);
		public int ToggleKey { get; set; } = 0x70; // F1 default

		// Crosshair
		public bool ShowLines { get; set; } = true;
		public bool ShowDot { get; set; } = true;
		public float Thickness { get; set; } = 2.0f;
		public float DotSize { get; set; } = 3.0f;
		public float Length { get; set; } = 10.0f;
		public float Gap { get; set; } = 5.0f;
		public bool TShape { get; set; } = false;
		public bool Outline { get; set; } = true;
		public Vector4 CrosshairColor { get; set; } = new Vector4(0, 1, 0, 1);
		public bool RainbowCrosshair { get; set; } = false;
		public float RainbowCrossSpeed { get; set; } = 1.0f;

		// FOV
		public bool ShowFov { get; set; } = false;
		public float FovSize { get; set; } = 100.0f;
		public Vector4 FovColor { get; set; } = new Vector4(1, 1, 1, 1);
		public bool RainbowFov { get; set; } = false;
		public float RainbowFovSpeed { get; set; } = 1.0f;
	}

	public class CrosshairOverlay : Overlay
	{
		private bool _showMenu = true;
		private bool _showAll = true;
		private bool _isInsertedPressed = false;
		private bool _isTogglePressed = false;

		// --- AUTO RESOLUTION VARIABLES ---
		private int _screenWidth;
		private int _screenHeight;
		private Vector2 _screenCenter;

		private CrosshairSettings s = new CrosshairSettings();
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };

		private string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
		private List<string> configList = new List<string>();
		private string inputConfigName = "";
		private int selectedIndex = -1;
		private bool isBinding = false;

		[DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
		[DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
		[DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
		[DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
		[DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
		[DllImport("user32.dll")] static extern int GetKeyNameText(int lParam, [Out] System.Text.StringBuilder lpString, int nSize);
		[DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);
		[DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

		public CrosshairOverlay() : base(GetSystemMetrics(0), GetSystemMetrics(1))
		{
			// Détection automatique de l'écran principal
			_screenWidth = GetSystemMetrics(0);
			_screenHeight = GetSystemMetrics(1);
			_screenCenter = new Vector2(_screenWidth / 2f, _screenHeight / 2f);

			if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
			RefreshConfigList();
			this.VSync = true;
		}

		private Vector4 GetRainbowColor(float speed)
		{
			float time = (float)DateTime.Now.TimeOfDay.TotalSeconds * speed;
			float r = (float)Math.Sin(time) * 0.5f + 0.5f;
			float g = (float)Math.Sin(time + 2.0f) * 0.5f + 0.5f;
			float b = (float)Math.Sin(time + 4.0f) * 0.5f + 0.5f;
			return new Vector4(r, g, b, 1.0f);
		}

		private string GetKeyName(int vKey)
		{
			if (vKey >= 0x05 && vKey <= 0x06) return vKey == 0x05 ? "Mouse 4" : "Mouse 5";
			uint scanCode = MapVirtualKey((uint)vKey, 0);
			long lParam = (long)scanCode << 16;
			if (vKey >= 0x21 && vKey <= 0x2E || vKey >= 0x25 && vKey <= 0x28) lParam |= 0x1000000;
			System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
			return GetKeyNameText((int)lParam, sb, 256) > 0 ? sb.ToString() : "Key " + vKey;
		}

		private void RefreshConfigList()
		{
			configList.Clear();
			if (Directory.Exists(configDir))
				foreach (var f in Directory.GetFiles(configDir, "*.json"))
					configList.Add(Path.GetFileNameWithoutExtension(f));
		}

		private void ApplyStyle()
		{
			var style = ImGui.GetStyle();
			style.WindowRounding = 0f;
			style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.08f, 1f);
			style.Colors[(int)ImGuiCol.Border] = s.MenuColor;
			style.Colors[(int)ImGuiCol.TitleBgActive] = s.MenuColor;
			style.Colors[(int)ImGuiCol.TitleBg] = s.MenuColor * 0.5f;
			style.Colors[(int)ImGuiCol.CheckMark] = s.MenuColor;
			style.Colors[(int)ImGuiCol.SliderGrab] = s.MenuColor;
			style.Colors[(int)ImGuiCol.ButtonActive] = s.MenuColor;
			style.Colors[(int)ImGuiCol.TabHovered] = s.MenuColor * 0.8f;
		}

		protected override void Render()
		{
			// --- NVIDIA FIX : GHOST MODE (S'exécute à la première frame) ---
			if (ImGui.GetFrameCount() == 1)
			{
				IntPtr hwnd = GetActiveWindow();
				// Flags: Layered (0x80000) | Transparent (0x20) | NoActivate (0x08000000)
				uint exStyle = GetWindowLong(hwnd, -20);
				SetWindowLong(hwnd, -20, exStyle | 0x80000 | 0x20 | 0x08000000);

				// Force au premier plan sur la résolution détectée
				SetWindowPos(hwnd, new IntPtr(-1), 0, 0, _screenWidth, _screenHeight, 0x0040);
			}

			ApplyStyle();

			if ((GetAsyncKeyState(0x2D) & 0x8000) != 0) { if (!_isInsertedPressed) { _showMenu = !_showMenu; _isInsertedPressed = true; } } else { _isInsertedPressed = false; }

			if (isBinding)
			{
				for (int i = 0x01; i < 0xFE; i++)
				{
					if ((GetAsyncKeyState(i) & 0x8000) != 0 && i != 0x01 && i != 0x2D)
					{
						s.ToggleKey = i; isBinding = false; break;
					}
				}
			}
			if (!isBinding && (GetAsyncKeyState(s.ToggleKey) & 0x8000) != 0) { if (!_isTogglePressed) { _showAll = !_showAll; _isTogglePressed = true; } } else { _isTogglePressed = false; }

			if (_showMenu)
			{
				ImGui.SetNextWindowSize(new Vector2(550, 520), ImGuiCond.Once);
				if (ImGui.Begin("Crosshair Editor | made by xkeishii", ref _showMenu, ImGuiWindowFlags.NoCollapse))
				{
					if (ImGui.BeginTabBar("Tabs"))
					{
						if (ImGui.BeginTabItem("Crosshair"))
						{
							ImGui.Columns(2, "split", false);
							ImGui.TextColored(s.MenuColor, "CONFIGURATION");
							bool sl = s.ShowLines; if (ImGui.Checkbox("Lines", ref sl)) s.ShowLines = sl;
							bool sd = s.ShowDot; if (ImGui.Checkbox("Center Dot", ref sd)) s.ShowDot = sd;
							bool so = s.Outline; if (ImGui.Checkbox("Black Outline", ref so)) s.Outline = so;
							bool st = s.TShape; if (ImGui.Checkbox("T-Shape Crosshair", ref st)) s.TShape = st;

							ImGui.Separator();
							bool rc = s.RainbowCrosshair; if (ImGui.Checkbox("Rainbow Mode", ref rc)) s.RainbowCrosshair = rc;
							if (s.RainbowCrosshair)
							{
								float rcs = s.RainbowCrossSpeed; if (ImGui.SliderFloat("Speed", ref rcs, 0.1f, 5.0f)) s.RainbowCrossSpeed = rcs;
							}

							ImGui.NextColumn();
							ImGui.TextColored(s.MenuColor, "ADJUSTMENTS");
							// FIX CS0206: Utilisation de variables locales pour éviter les erreurs de propriétés
							float th = s.Thickness; if (ImGui.SliderFloat("Thickness", ref th, 1, 10)) s.Thickness = th;
							float ln = s.Length; if (ImGui.SliderFloat("Length", ref ln, 1, 50)) s.Length = ln;
							float gp = s.Gap; if (ImGui.SliderFloat("Gap", ref gp, 0, 30)) s.Gap = gp;
							float ds = s.DotSize; if (ImGui.SliderFloat("Dot Size", ref ds, 1, 10)) s.DotSize = ds;

							if (!s.RainbowCrosshair)
							{
								Vector4 cp = s.CrosshairColor; if (ImGui.ColorEdit4("Crosshair Color", ref cp, ImGuiColorEditFlags.NoInputs)) s.CrosshairColor = cp;
							}

							ImGui.Columns(1);
							ImGui.EndTabItem();
						}

						if (ImGui.BeginTabItem("FOV Settings"))
						{
							ImGui.TextColored(s.MenuColor, "CIRCLE SETTINGS");
							bool sf = s.ShowFov; if (ImGui.Checkbox("Show FOV Circle", ref sf)) s.ShowFov = sf;
							float fs = s.FovSize; if (ImGui.SliderFloat("FOV Size", ref fs, 10, 800)) s.FovSize = fs;

							ImGui.Separator();
							bool rf = s.RainbowFov; if (ImGui.Checkbox("Rainbow FOV", ref rf)) s.RainbowFov = rf;
							if (s.RainbowFov)
							{
								float rfs = s.RainbowFovSpeed; if (ImGui.SliderFloat("Rainb. Speed", ref rfs, 0.1f, 5.0f)) s.RainbowFovSpeed = rfs;
							}
							else
							{
								Vector4 fc = s.FovColor; if (ImGui.ColorEdit4("FOV Color", ref fc, ImGuiColorEditFlags.NoInputs)) s.FovColor = fc;
							}
							ImGui.EndTabItem();
						}

						if (ImGui.BeginTabItem("Config"))
						{
							ImGui.TextColored(s.MenuColor, "PROFILE MANAGEMENT");
							ImGui.InputText("Profile Name", ref inputConfigName, 32);
							if (ImGui.Button("Create & Save New", new Vector2(-1, 30)))
							{
								if (!string.IsNullOrWhiteSpace(inputConfigName))
								{
									File.WriteAllText(Path.Combine(configDir, inputConfigName + ".json"), JsonSerializer.Serialize(s, jsonOptions));
									RefreshConfigList(); inputConfigName = "";
								}
							}
							ImGui.Separator();
							if (ImGui.BeginListBox("##list", new Vector2(-1, 150)))
							{
								for (int i = 0; i < configList.Count; i++) if (ImGui.Selectable(configList[i], selectedIndex == i)) selectedIndex = i;
								ImGui.EndListBox();
							}
							if (selectedIndex != -1)
							{
								string path = Path.Combine(configDir, configList[selectedIndex] + ".json");
								if (ImGui.Button("Execute", new Vector2(120, 30))) s = JsonSerializer.Deserialize<CrosshairSettings>(File.ReadAllText(path), jsonOptions);
								ImGui.SameLine();
								if (ImGui.Button("Save", new Vector2(120, 30))) File.WriteAllText(path, JsonSerializer.Serialize(s, jsonOptions));
								ImGui.SameLine();
								if (ImGui.Button("Delete", new Vector2(120, 30))) { File.Delete(path); RefreshConfigList(); selectedIndex = -1; }
							}
							ImGui.EndTabItem();
						}

						if (ImGui.BeginTabItem("Menu Settings"))
						{
							ImGui.TextColored(s.MenuColor, "APPEARANCE");
							Vector4 mc = s.MenuColor; if (ImGui.ColorEdit4("Main Theme Color", ref mc, ImGuiColorEditFlags.NoInputs)) s.MenuColor = mc;
							ImGui.Separator();
							ImGui.TextColored(s.MenuColor, "CROSSHAIR TOGGLE");
							ImGui.Text($"Current Toggle Key: {GetKeyName(s.ToggleKey)}");
							if (ImGui.Button(isBinding ? "Press any key..." : "Change Bind", new Vector2(-1, 40))) isBinding = true;

							ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 60);
							ImGui.TextDisabled($"Resolution Detectée : {_screenWidth}x{_screenHeight}");
							ImGui.TextDisabled("Appuyez sur INSERT pour cacher/afficher ce menu");
							ImGui.EndTabItem();
						}
						ImGui.EndTabBar();
					}
				}
				ImGui.End();
			}
			if (_showAll) DrawCrosshair();
		}

		private void DrawCrosshair()
		{
			// Utilise le centre automatique au lieu de 2560/1440 en dur
			Vector2 center = _screenCenter;
			var dl = ImGui.GetForegroundDrawList();

			Vector4 finalCrossColor = s.RainbowCrosshair ? GetRainbowColor(s.RainbowCrossSpeed) : s.CrosshairColor;
			Vector4 finalFovColor = s.RainbowFov ? GetRainbowColor(s.RainbowFovSpeed) : s.FovColor;

			uint col = ImGui.ColorConvertFloat4ToU32(finalCrossColor);
			uint outCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1));

			if (s.ShowFov) dl.AddCircle(center, s.FovSize, ImGui.ColorConvertFloat4ToU32(finalFovColor), 100, 1.5f);
			if (s.ShowDot)
			{
				if (s.Outline) dl.AddCircleFilled(center, s.DotSize + 1.0f, outCol);
				dl.AddCircleFilled(center, s.DotSize, col);
			}
			if (s.ShowLines)
			{
				void DrawL(Vector2 start, Vector2 end)
				{
					if (s.Outline) dl.AddLine(start, end, outCol, s.Thickness + 2.0f);
					dl.AddLine(start, end, col, s.Thickness);
				}
				DrawL(new Vector2(center.X, center.Y + s.Gap), new Vector2(center.X, center.Y + s.Gap + s.Length));
				if (!s.TShape) DrawL(new Vector2(center.X, center.Y - s.Gap), new Vector2(center.X, center.Y - s.Gap - s.Length));
				DrawL(new Vector2(center.X + s.Gap, center.Y), new Vector2(center.X + s.Gap + s.Length, center.Y));
				DrawL(new Vector2(center.X - s.Gap, center.Y), new Vector2(center.X - s.Gap - s.Length, center.Y));
			}
		}
	}

	class Program
	{
		[DllImport("user32.dll")] static extern bool SetProcessDPIAware();
		[STAThread]
		static void Main()
		{
			// Indispensable pour que le calcul du centre soit exact peu importe le scaling Windows
			SetProcessDPIAware();
			new CrosshairOverlay().Start().Wait();
		}
	}
}