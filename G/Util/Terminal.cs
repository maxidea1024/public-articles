using System;

namespace G.Util
{
	public static class Terminal
	{
		public static void Up(int n)
		{
			Console.Write("\x1b[{0}A", n);
		}

		public static void Down(int n)
		{
			Console.Write("\x1b[{0}B", n);
		}

		public static void Forward(int n)
		{
			Console.Write("\x1b[{0}C", n);
		}

		public static void Back(int n)
		{
			Console.Write("\x1b[{0}D", n);
		}

		public static void MoveLine(int n)			// Move to Beginning of the Line
		{
			if (n < 0)
				Console.Write("\x1b[{0}F", -n);
			else
				Console.Write("\x1b[{0}E", n);
		}

		public static void MoveHorizontal(int n)
		{
			Console.Write("\x1b[{0}G", n);
		}

		public static void Position(int row, int col)
		{
			Console.Write("\x1b[{0};{1}H", row, col);
		}

		public static void EraseDisplay(int n)		// 0:from Cursor to End of screen, 1:from Cursor to Begin of screen, 2:Entire screen
		{
			Console.Write("\x1b[{0}J", n);
		}

		public static void EraseLine(int n)			// 0:from Cursor to End of line, 1:from Cursor to Begin of line, 2:Entire line
		{
			Console.Write("\x1b[{0}K", n);
		}

		public static void ScrollUp(int n)
		{
			Console.Write("\x1b[{0}S", n);
		}

		public static void ScrollDown(int n)
		{
			Console.Write("\x1b[{0}T", n);
		}

		public static void Move(int row, int col)
		{
			Console.Write("\x1b[{0};{1}f", row, col);
		}

		public static void Reset()
		{
			Console.Write("\x1b[0m");
		}

		public static void Bold(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[1m");
			else
				Console.Write("\x1b[21m");
		}

		public static void Faint()
		{
			Console.Write("\x1b[2m");
		}

		public static void Italic(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[3m");
			else
				Console.Write("\x1b[23m");
		}

		public static void UnderLine(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[4m");
			else
				Console.Write("\x1b[24m");
		}

		public static void BlinkSlow()
		{
			Console.Write("\x1b[5m");
		}

		public static void BlinkRapid()
		{
			Console.Write("\x1b[6m");
		}

		public static void BlinkOff()
		{
			Console.Write("\x1b[25m");
		}

		public static void ImageNegative()
		{
			Console.Write("\x1b[7m");
		}

		public static void ImagePositive()
		{
			Console.Write("\x1b[27m");
		}

		public static void Conceal()
		{
			Console.Write("\x1b[8m");
		}

		public static void Reveal()
		{
			Console.Write("\x1b[28m");
		}

		public static void CrossedOut(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[9m");
			else
				Console.Write("\x1b[29m");
		}

		public static void SetFont(int n = 0)		// 1 ~ 9
		{
			n += 10;
			Console.Write("\x1b[{0}m", n);
		}

		public static void SetNormal()
		{
			Console.Write("\x1b[22m");
		}

		public static void SetTextColor(int colorIndex, bool bold = false)		// 0 ~ 15
		{
			colorIndex += ((colorIndex < 8) ? 30 : 82);

			if (bold)
				Console.Write("\x1b[{0};1m", colorIndex);
			else
				Console.Write("\x1b[{0}m", colorIndex);
		}

		public static void SetTextColor(int red, int green, int blue)			// 0 ~ 255
		{
			Console.Write("\x1b[38;2;{0};{1};{2}m", red, green, blue);
		}

		public static void SetTextColor256(int colorIndex)						// 0 ~ 255
		{
			Console.Write("\x1b[38;5;{0}m", colorIndex);
		}

		public static void ResetColor()
		{
			Console.Write("\x1b[39;49m");
		}

		public static void ResetTextColor()
		{
			Console.Write("\x1b[39m");
		}

		public static void ResetBackgroundColor()
		{
			Console.Write("\x1b[49m");
		}

		public static void SetBackgroundColor(int colorIndex)					// 0 ~ 15
		{
			colorIndex += ((colorIndex < 8) ? 40 : 92);
			Console.Write("\x1b[{0}m", colorIndex);
		}

		public static void SetBackgroundColor(int red, int green, int blue)		// 0 ~ 255
		{
			Console.Write("\x1b[48;2;{0};{1};{2}m", red, green, blue);
		}

		public static void SetBackgroundColor256(int colorIndex)				// 0 ~ 255
		{
			Console.Write("\x1b[48;5;{0}m", colorIndex);
		}

		public static void Framed(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[51m");
			else
				Console.Write("\x1b[54m");
		}

		public static void Encircled(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[52m");
			else
				Console.Write("\x1b[54m");
		}

		public static void Overlined(bool flag = true)
		{
			if (flag)
				Console.Write("\x1b[53m");
			else
				Console.Write("\x1b[55m");
		}

		public static void EnableAux()
		{
			Console.Write("\x1b[5i");
		}

		public static void DisableAux()
		{
			Console.Write("\x1b[4i");
		}

		public static void DeviceStatusReport()
		{
			Console.Write("\x1b[6n");
		}

		public static void SaveCursorPosition()
		{
			Console.Write("\x1b[s");
		}

		public static void RestoreCursorPosition()
		{
			Console.Write("\x1b[u");
		}

		public static void HideCursor()
		{
			Console.Write("\x1b[?25l");
		}

		public static void ShowCursor()
		{
			Console.Write("\x1b[?25h");
		}
	}
}
