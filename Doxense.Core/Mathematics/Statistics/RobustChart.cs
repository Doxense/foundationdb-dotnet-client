#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Mathematics.Statistics
{
	using System.Globalization;
	using System.Text;

	/// <summary>Generates ASCII Art that look like plots</summary>
	[PublicAPI]
	public static class RobustChart
	{

		#region Samples
		//
		// Page counter (will null values marked as 'X'):
		//
		// 1,156,800 +--------------+--------------+--------------+-------------+
		//           |              :              :              :             |
		//           |              :              :              :        ######
		//           |              :########################## ###########     |
		// 1,156,650 +.............##..............:..............:.............+
		//           |           ## :              :              :             |
		//           |          #   :              :              :             |
		//           |         #    :              :              :             |
		//           |              :              :              :             |
		// 1,156,500 +.......#.....................:............................+
		//           |      #       :              :              :             |
		//           |     #        :              :              :             |
		//           |    #         :              :              :             |
		//           |   #          :              :              :             |
		// 1,156,350 +..............:..............:..............:.............+
		//           |  #           :              :              :             |
		//           | #            :              :              :             |
		//           |#             :              :              :             |
		//           #              :              :              :             |
		// 1,156,200 +--------X-----+--------------+-----------X--+-------------+
		//
		// Load avarage (0..1):
		//
		//       0.4 +--------------+--------------+--------------+-------------+
		//           |              :              :              :             |
		//           |              :              :              :             |
		//           |              :   #          :              :             |
		//       0.3 +..............:..............:..............:.............+
		//           |  #           :              :              :             |
		//           |              :              :              :      #    # |
		//           |              :              :              :             |
		//           |              :              :              :             |
		//       0.2 +#........##..................:...#.....#..................+
		//           |    ##        #     #        :    #         :   #     #   |
		//           |              :              :              :             |
		//           |   #    #     :    #        #:          #   : #  #        |
		//           | #    #       : #     ###    :# #  #  #   # :        #   #|
		//       0.1 +...........###:#.#.........#.:.#....##...#.##.....#.......+
		//           |              :              :              :             |
		//           #              :      #   #   #              :#      #  #  |
		//           |              :           #  :              :  #          #
		//           |       #      :              :              :             |
		//       0.0 +--------------+--------------+--------------+-------------+
		//
		#endregion
		
		/// <summary>Buffer permettant de dessiner sur une frame</summary>
		[DebuggerDisplay("Size={Width}x{Height}")]
		[PublicAPI]
		public readonly struct FrameBuffer : IFormattable
		{

			// Frame contenant une grille de "texel" (~ pixel représentés par des caractères ASCII)
			// - Width et Height correspondant à la larger et hauteur du buffer en texel
			// - L'axe X va de droite à gauche
			// - L'axe Y va de bas en haut (inversé par rpt à une bitmap classique, mais dans le "bon sens" pour des graphes)
			// => (0, 0) correspond au texel en bas a gauche, (Width-1, Height-1) au texel en haut à droite

			/// <summary>Largeur du buffer (en texels)</summary>
			public readonly int Width;

			/// <summary>Hauteur du buffer (en texels)</summary>
			public readonly int Height;

			/// <summary>Tableau contenant les texels, lignes par lignes, DE HAUT EN BAS!</summary>
			/// <remarks>Le texel (X, Y) est situé à l'offset (HEIGHT - Y - 1) * WIDTH + X</remarks>
			public readonly char[] Buffer;

			public FrameBuffer(int width, int height)
			{
				Contract.GreaterThan(width, 0);
				Contract.GreaterThan(height, 0);

				this.Width = width;
				this.Height = height;
				var buffer = new char[width * height];
				for (int i = 0; i < buffer.Length; i++) buffer[i] = ' ';
				this.Buffer = buffer;
			}

			public FrameBuffer(char[] buffer, int width, int height)
			{
				Contract.NotNull(buffer);
				Contract.GreaterThan(width, 0);
				Contract.GreaterThan(height, 0);
				if (buffer.Length != width * height) throw new InvalidOperationException("Buffer size must be equal to width times height.");

				this.Width = width;
				this.Height = height;
				this.Buffer = buffer;
			}

			/// <summary>Convertit une coordonnée X vers l'échelle du buffer</summary>
			/// <param name="x">Coordonnée 0 &lt;= x &lt; WIDTH</param>
			/// <returns>Coordonnée X arrondi en entier, dans l'espace du buffer</returns>
			[Pure]
			private int RoundX(double x) => Math.Min(this.Width - 1, Math.Max(0, (int)Math.Floor(x)));

			/// <summary>Convertit une coordonnée Y vers l'échelle du buffer</summary>
			/// <param name="y">Coordonnée 0 &lt;= y &lt; HEIGHT (0 en bas)</param>
			/// <returns>Coordonnée Y arrondi en entier, dans l'espace du buffer (0 en haut)</returns>
			[Pure]
			private int RoundY(double y) => this.Height - 1 - Math.Min(this.Height - 1, Math.Max(0, (int)Math.Floor(y)));

			/// <summary>Clip une coordonnée X en fonction de la largeur du buffer</summary>
			/// <returns>0 si <paramref name="x"/> &lt; 0, ou WIDTH-1 si <paramref name="x"/> &gt;= WIDTH</returns>
			[Pure]
			private int BoundX(int x) => Math.Min(this.Width - 1, Math.Max(0, x));

			/// <summary>Clip une coordonnée Y en fonction de la hauteur du buffer</summary>
			/// <returns>0 si <paramref name="y"/> &lt; 0, ou HEIGHT-1 si <paramref name="y"/> &gt;= HEIGHT</returns>
			[Pure]
			private int BoundY(int y) => this.Height - 1 - Math.Min(this.Height - 1, Math.Max(0, y));

			/// <summary>Modifie la valeur d'un texel aux coordonnées indiquées</summary>
			/// <param name="x">Coordonnée X (0 à gauche), qui sera arrondie vers la gauche</param>
			/// <param name="y">Coordonnée Y (0 en bas), qui sera arrondie vers la bas</param>
			/// <param name="c">"Couleur" du texel</param>
			public void Set(double x, double y, char c)
			{
				int xx = RoundX(x);
				int yy = RoundY(y);
				int w = this.Width;
				if (Math.Abs(xx) < w && Math.Abs(yy) < this.Height)
				{
					this.Buffer[yy * w + xx] = c;
				}
			}

			/// <summary>Modifie la valeur d'un texel aux coordonnées indiquées</summary>
			/// <param name="x">Coordonnée X (0 à gauche)</param>
			/// <param name="y">Coordonnée Y (0 en bas)</param>
			/// <param name="c">"Couleur" du texel</param>
			public void Set(int x, int y, char c)
			{
				int w = this.Width;
				y = this.Height - 1 - y;
				if (Math.Abs(x) < w && Math.Abs(y) < this.Height)
				{
					this.Buffer[y * w + x] = c;
				}
			}

			/// <summary>Dessine une ligne horizontale</summary>
			/// <param name="x0">Coordonnée X gauche</param>
			/// <param name="y0">Coordonnée Y</param>
			/// <param name="x1">Coordonnée Y droite</param>
			/// <param name="c">"Couleur" de la ligne</param>
			public void HorizontalLine(double x0, double y0, double x1, char c = '.')
			{
				int cx0 = RoundX(x0);
				int cx1 = RoundX(x1);
				if (cx0 <= cx1)
				{
					int cy0 = RoundY(y0);
					int p = cy0 * this.Width;
					var buffer = this.Buffer;
					for (int i = cx0; i <= cx1; i++)
					{
						buffer[p + i] = c;
					}
				}
			}

			/// <summary>Dessine une ligne horizontale</summary>
			/// <param name="x0">Coordonnée X</param>
			/// <param name="y0">Coordonnée Y basse</param>
			/// <param name="y1">Coordonnée Y haute</param>
			/// <param name="c">"Couleur" de la ligne</param>
			public void VerticalLine(double x0, double y0, double y1, char c = ':')
			{
				int cy0 = RoundY(y0);
				int cy1 = RoundY(y1);
				if (cy0 >= cy1) //note: y is reversed!
				{
					int cx0 = RoundX(x0);
					int w = this.Width;
					var buffer = this.Buffer;
					for (int i = cy1; i <= cy0; i++) //note: y is reversed!
					{
						buffer[i * w + cx0] = c;
					}
				}
			}

			/// <summary>Ecrit du texte</summary>
			public void DrawText(int x, int y, string text, int align = 0)
			{
				int l = align > 0 ? align - text.Length : 0;
				for (int i = 0; i < text.Length; i++)
				{
					Set(x + i + l, y, text[i]);
				}
			}

			/// <summary>Copie un buffer dans un autre buffer</summary>
			public void DrawFrame(int x, int y, FrameBuffer image)
			{
				Contract.Positive(x);
				Contract.Positive(y);
				if (x >= this.Width || y >= this.Height) return;
				int w = Math.Min(this.Width - x, image.Width);
				int h = Math.Min(this.Height - y, image.Height);

				var dst = this.Buffer;
				var src = image.Buffer;
				for (int j = 0; j < h; j++)
				{
					int offSrc = (image.Height - y - j - 1) * image.Width;
					int offDest = (this.Height - j - 1) * this.Width;
					for (int i = 0; i < w; i++)
					{
						dst[offDest + x + i] = src[offSrc + i];
					}
				}
			}

			public void DrawXAxis(int w, int x0 = 0, int y0 = 0, char c = '-', char start = '+', char end = '>')
			{
				HorizontalLine(x0, y0, x0 + w, c);
				if (w > 2 && start != '\0') Set(x0, y0, start);
				if (w > 1 && end != 0) Set(x0 + w - 1, y0, end);
			}

			public void DrawXGridLine(int w, int x0 = 0, int y0 = 0, char c = '.', char start = '+', char end = '\0')
			{
				if (w > 1) Set(x0, y0, start != '\0' ? start : c);
				if (w > 2) HorizontalLine(x0 + 1, y0, x0 + w - 2, c);
				if (w > 0) Set(x0 + w - 1, y0, end != '\0' ? end : c);
			}

			public void DrawYAxis(int h, int x0 = 0, int y0 = 0, char c = '|', char start = '+', char end = '^')
			{
				VerticalLine(x0, y0, y0 + h, c);
				if (h > 2 && start != '\0') Set(x0, y0, start);
				if (h > 1 && end != '\0') Set(x0, y0 + h - 1, end);
			}

			public void DrawYGridLine(int h, int x0 = 0, int y0 = 0, char c = ':', char start = '+', char end = '\0')
			{
				if (h > 1) Set(x0, y0, start != '\0' ? start : c);
				if (h > 2) VerticalLine(x0, y0 + 1, y0 + h - 2, c);
				if (h > 0) Set(x0, y0 + h - 1, end != '\0' ? end : c);
			}

			[Pure]
			public static FrameBuffer CombineHorizontal(FrameBuffer left, FrameBuffer right, int pad = 0)
			{
				if (left.Height != right.Height) throw new InvalidOperationException("Both frames must have the same height");
				var frame = new FrameBuffer(left.Width + pad + right.Width, left.Height);
				frame.DrawFrame(0, 0, left);
				frame.DrawFrame(pad + left.Width, 0, right);
				return frame;
			}

			[Pure]
			public static FrameBuffer CombineVertical(FrameBuffer top, FrameBuffer bottom, int pad = 0)
			{
				if (top.Width != bottom.Width) throw new InvalidOperationException("Both frames must have the same width");
				var frame = new FrameBuffer(top.Width, top.Height + pad + bottom.Height);
				frame.DrawFrame(0, bottom.Height + pad, top);
				frame.DrawFrame(0, 0, bottom);
				return frame;
			}

			/// <summary>Ecrit le buffer dans une StringBuilder</summary>
			/// <param name="output"></param>
			/// <param name="prefix">Texte ajouté au début de chaque ligne (utilisé pour simuler une indentation)</param>
			/// <param name="suffix">Texte ajouté en fin de chaque ligne (utilisé pour injecter une frame et/ou une fin de ligne spécifique)</param>
			public void Output(StringBuilder output, string? prefix = null, string suffix = "\r\n")
			{
				Contract.Debug.Requires(output != null);
				int w = this.Width, h = this.Height, ofs = 0;
				var buffer = this.Buffer;
				for (int i = 0; i < h; i++)
				{
					output.Append(prefix).Append(buffer, ofs, w).Append(suffix);
					ofs += w;
				}
			}

			/// <summary>Retourne le buffer sous forme de texte multi-ligne</summary>
			public override string ToString()
			{
				var sb = new StringBuilder((this.Width + 2) * this.Height);
				Output(sb);
				return sb.ToString();
			}

			[Pure]
			public string ToString(string? format)
			{
				return ToString(format, null);
			}

			public string ToString(string? format, IFormatProvider? formatProvider)
			{
				switch (format ?? "D")
				{
					case "D": case "d":
						return ToString();
					default:
						throw new ArgumentException("Invalid format specified.", nameof(format));
				}
			}
		}

		/// <summary>Série de valeurs</summary>
		[DebuggerDisplay("Count={Count}; Name={Name}")]
		[PublicAPI]
		public readonly struct Data
		{

			/// <summary>Liste des valeurs de cette série</summary>
			public readonly double?[] Values;

			public readonly string? Name;

			#region Constructors...

			public Data(double?[] values, string? name = null)
			{
				this.Values = values;
				this.Name = null;
			}

			/// <summary>Convertit une séquence d'éléments en série de données</summary>
			[Pure]
			public static Data Convert<T>(IEnumerable<T> values, [InstantHandle] Func<T, double> transform, string? name = null)
			{
				Contract.NotNull(values);
				Contract.NotNull(transform);

				if (values is ICollection<T> coll)
				{
					var xs = new double?[coll.Count];
					int p = 0;
					foreach (var x in values)
					{
						xs[p++] = transform(x);
					}
					return new Data(xs, name);
				}
				else
				{
					var xs = new List<double?>();
					foreach (var x in values)
					{
						xs.Add(transform(x));
					}
					return new Data(xs.ToArray(), name);
				}
			}

			/// <summary>Convertit une séquence d'éléments en série de données</summary>
			[Pure]
			public static Data Convert<T>(IEnumerable<T> values, [InstantHandle] Func<T, double?> transform, string? name = null)
			{
				Contract.NotNull(values);
				Contract.NotNull(transform);

				if (values is ICollection<T> coll)
				{
					var xs = new double?[coll.Count];
					int p = 0;
					foreach (var x in values)
					{
						xs[p++] = transform(x);
					}
					return new Data(xs, name);
				}

				return new Data(values.Select(transform).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(int[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<int> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(int?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<int?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(uint[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<uint> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(uint?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<uint?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(long[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<long> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(long?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<long?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(ulong[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<ulong> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(ulong?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence d'entiers en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<ulong?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence de nombres décimaux en série de données</summary>
			[Pure]
			public static Data Convert(double[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence de nombres décimaux en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<double> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Convertit une séquence de nombres décimaux en série de données</summary>
			[Pure]
			public static Data Convert(double?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				Array.Copy(values, 0, xs, 0, xs.Length);
				return new Data(xs, name);
			}

			/// <summary>Convertit une séquence de nombres décimaux en série de données</summary>
			[Pure]
			public static Data Convert(IEnumerable<double?> values, string? name = null)
			{
				return new Data(values.ToArray(), name);
			}

			public Data Convert(Func<double?, double?> transform)
			{
				return new Data(this.Values.Select(transform).ToArray(), this.Name);
			}

			public Data Log()
			{
				return new Data(this.Values.Select(x => x.HasValue ? Math.Log(x.Value) : default(double?)).ToArray(), this.Name);
			}

			public Data Log10()
			{
				return new Data(this.Values.Select(x => x.HasValue ? Math.Log10(x.Value) : default(double?)).ToArray(), this.Name);
			}

			#endregion

			public int Count => this.Values.Length;

			/// <summary>Retourne la valeur minimale</summary>
			/// <remarks>Peut retourner null si tous les points sont null</remarks>
			public double? Min()
			{
				var min = default(double?);
				foreach (var v in this.Values)
				{
					if (v != null && (min == null || min.Value > v.Value)) min = v;
				}
				return min;
			}

			/// <summary>Retourne la valeur maximale</summary>
			/// <remarks>Peut retourner null si tous les points sont null</remarks>
			public double? Max()
			{
				var max = default(double?);
				foreach (var v in this.Values)
				{
					if (v != null && (max == null || max.Value < v.Value)) max = v;
				}
				return max;
			}

			/// <summary>Indique s'il y a au moins une valeur nulle</summary>
			public bool HasNulls()
			{
				foreach (var v in this.Values)
				{
					if (v == null) return true;
				}
				return false;
			}

			/// <summary>Génère une nouvelle série de donnés contenant la dérivée de cette série</summary>
			/// <returns>Série de données de taille N-1, ou RESULT[i] = THIS[i+1] - THIS[i]</returns>
			[Pure]
			public Data Derive()
			{
				var vals = this.Values;
				var d = new double?[vals.Length - 1];
				for (int i = 1; i < vals.Length; i++)
				{
					d[i - 1] = vals[i] - vals[i - 1];
				}
				return new Data(d, this.Name != null ? (this.Name + " (d/dt)") : null);
			}

			/// <summary>Remplace tous les 0 par des null</summary>
			[Pure]
			public Data WithoutZero()
			{
				var vals = this.Values;
				var d = new double?[vals.Length];
				for (int i = 0; i < vals.Length; i++)
				{
					if (vals[i] > 0) d[i] = vals[i];
				}
				return new Data(d, this.Name);
			}
		}

		[PublicAPI]
		public static class Axis
		{
			[Pure]
			public static Func<double, double> Linear(double min, double max, double range)
			{
				// ReSharper disable once CompareOfFloatsByEqualityOperator
				double r = max == min ? 1 : (range / (max - min));
				return (x) => (x - min) * r;
			}

		}

		public static void DrawData(FrameBuffer frame, Data xs, [InstantHandle] Func<double, double> xAxis, [InstantHandle] Func<double, double> yAxis, int x0 = 0, int y0 = 0, char c = '*', char nc = 'x')
		{
			var values = xs.Values;
			for (int x = 0; x < values.Length; x++)
			{
				var y = values[x];
				if (y != null)
				{
					frame.Set(x0 + xAxis(x), y0 + yAxis(y.Value), c);
				}
				else if (nc != '\0')
				{
					frame.Set(x0 + xAxis(x), y0, nc);
				}
			}
		}

		public static void DrawData(FrameBuffer frame, Data xs, Data ys, [InstantHandle] Func<double, double> xAxis, [InstantHandle] Func<double, double> yAxis, int x0 = 0, int y0 = 0, char c = '*', char nc = 'x')
		{
			int n = xs.Count;
			for (int idx = 0; idx < n; idx++)
			{
				var x = xs.Values[idx];
				if (x == null) continue;
				var y = ys.Values[idx];
				if (y != null)
				{
					frame.Set(x0 + xAxis(x.Value), y0 + yAxis(y.Value), c);
				}
				else if (nc != '\0')
				{
					frame.Set(x0 + xAxis(x.Value), y0, nc);
				}
			}
		}

		/// <summary>Arrondi un valeur vers le bas, en fonction de l'arrondi sélectionné</summary>
		/// <param name="x">Valeur à arrondir</param>
		/// <param name="digits">Si positif, nombre de décimales. Si négatif, nombre de digits entiers supprimés</param>
		/// <returns>Nombre &lt;= à <paramref name="x"/></returns>
		/// <example>
		/// - SigFloor(PI, 2) => 3.14
		/// - SigFloor(123456789, 3) => 123,456,000
		/// </example>
		[Pure]
		public static double SigFloor(double x, int digits)
		{
			if (digits > 0)
			{
				double pow = Math.Pow(10, digits);
				return Math.Floor(x * pow) / pow;
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return Math.Floor(x / pow) * pow;
			}
			return Math.Floor(x);
		}

		/// <summary>Calcule la valeur vers le haut, en fonction de l'arrondi sélectionné</summary>
		/// <param name="x">Valeur à arrondir</param>
		/// <param name="digits">Si positif, nombre de décimales. Si négatif, nombre de digits entiers supprimés</param>
		/// <returns>Nombre &gt;= à <paramref name="x"/></returns>
		/// <example>
		/// - SigCeiling(PI, 2) => 3.15
		/// - SigCeiling(123456789, 3) => 123,457,000
		/// </example>
		[Pure]
		public static double SigCeiling(double x, int digits)
		{
			if (digits > 0)
			{
				double pow = Math.Pow(10, digits);
				return Math.Ceiling(x * pow) / pow;
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return Math.Ceiling(x / pow) * pow;
			}
			return Math.Ceiling(x);
		}

		/// <summary>Formate un nombre, avec gestion d'arrondi</summary>
		/// <param name="x">Valeur à formatter</param>
		/// <param name="digits">Si positif, nombre de décimales. Si négatif, nombre de digits entiers supprimés</param>
		/// <example>
		/// - SigFormat(PI, 3) => "3.145"
		/// - SigFormat(123456789, -3) => "123,456,000"
		/// </example>
		[Pure]
		public static string SigFormat(double x, int digits)
		{
			if (digits > 0)
			{
				return x.ToString("N" + digits, CultureInfo.InvariantCulture);
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return (Math.Ceiling(x / pow) * pow).ToString("N0", CultureInfo.InvariantCulture);
			}
			return x.ToString("N0", CultureInfo.InvariantCulture);
		}

		/// <summary>Render un Plot à partir d'une séries de données</summary>
		/// <param name="ys">Valeurs sur l'axe Y des points pour chaque X dans l'ensemble { 0, 1, .., N-1 }</param>
		/// <param name="width">Largeur (en caractères) de la zone de dessin (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="height">Hauteur (en caractères) de la frame (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="min">Valeur mini sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="max">Valeur maxi sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="digits">Nombre de digits utilisé pour l'arrondi. Si positif, nombre de décimales. Si négatif, arrondi en millers/millions, etc.. (ex: 2 pour 1.2345=>'1.23', ou -3 pour 123456=>'123000'</param>
		/// <param name="c">Caractère utilisés pour le dessin des points de la série</param>
		/// <returns>Texte correspondant au graph généré</returns>
		[Pure]
		public static string Render(Data ys, int width, int height, double? min = null, double? max = null, [Positive] int digits = 0, char c = '#')
		{
			return RenderFrame(ys, width, height, min, max, digits, c).ToString();
		}

		/// <summary>Render un Plot à partir d'une liste de séries de données</summary>
		/// <param name="series">Liste des séries de données. chaque série contient les valeurs sur l'axe Y des points pour X dans l'ensemble { 0, 1, .., N-1 }</param>
		/// <param name="width">Largeur (en caractères) de la zone de dessin (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="height">Hauteur (en caractères) de la frame (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="min">Valeur mini sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="max">Valeur maxi sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="digits">Nombre de digits utilisé pour l'arrondi. Si positif, nombre de décimales. Si négatif, arrondi en millers/millions, etc.. (ex: 2 pour 1.2345=>'1.23', ou -3 pour 123456=>'123000'</param>
		/// <param name="chars">String contenant les caractères utilisés pour le dessin des points de chaque série. <paramref name="chars"/>[0] est utilisé pour dessiner <paramref name="series"/>[0], etc... (modulo si pas assez de caractères)</param>
		/// <returns>Texte correspondant au graph généré</returns>
		[Pure]
		public static string Render(Data[] series, int width, int height, double? min = null, double? max = null, int digits = 0, string? chars = null)
		{
			return RenderFrame(series, width, height, min, max, digits, chars).ToString();
		}

		/// <summary>Render un Plot à partir d'une séries de données</summary>
		/// <param name="ys">Valeurs sur l'axe Y des points pour chaque X dans l'ensemble { 0, 1, .., N-1 }</param>
		/// <param name="width">Largeur (en caractères) de la zone de dessin (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="height">Hauteur (en caractères) de la frame (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="min">Valeur mini sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="max">Valeur maxi sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="digits">Nombre de digits utilisé pour l'arrondi. Si positif, nombre de décimales. Si négatif, arrondi en millers/millions, etc.. (ex: 2 pour 1.2345=>'1.23', ou -3 pour 123456=>'123000'</param>
		/// <param name="c">Caractère utilisés pour le dessin des points de la série</param>
		/// <returns>Buffer contenant le graph généré</returns>
		[Pure]
		public static FrameBuffer RenderFrame(Data ys, int width, int height, double? min = null, double? max = null, [Positive] int digits = 0, char c = '#')
		{
			var yMin = min ?? ys.Min().GetValueOrDefault();
			var yMax = max ?? ys.Max().GetValueOrDefault();
			return RenderFrame([ ys ], width, height, yMin, yMax, digits, new string(c, 1));
		}

		/// <summary>Render un Plot à partir d'une liste de séries de données</summary>
		/// <param name="series">Liste des séries de données. chaque série contient les valeurs sur l'axe Y des points pour chaque X dans l'ensemble { 0, 1, .., N-1 }</param>
		/// <param name="width">Largeur (en caractères) de la zone de dessin (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="height">Hauteur (en caractères) de la frame (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="min">Valeur mini sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="max">Valeur maxi sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="digits">Nombre de digits utilisé pour l'arrondi. Si positif, nombre de décimales. Si négatif, arrondi en millers/millions, etc.. (ex: 2 pour 1.2345=>'1.23', ou -3 pour 123456=>'123000'</param>
		/// <param name="chars">String contenant les caractères utilisés pour le dessin des points de chaque série. <paramref name="chars"/>[0] est utilisé pour dessiner <paramref name="series"/>[0], etc... (modulo si pas assez de caractères)</param>
		/// <returns>Buffer contenant le graph généré</returns>
		[Pure]
		public static FrameBuffer RenderFrame(Data[] series, int width, int height, double? min = null, double? max = null, int digits = 0, string? chars = null)
		{
			var yMin = SigFloor(min ?? series.Min(s => s.Min()) ?? 0, digits);
			var yMax = SigCeiling(max ?? series.Max(s => s.Max()) ?? 0, digits);
			int count = series[0].Count;

			int d = Math.Max(0, digits);
			int xMargin = 1 + Math.Max(SigFormat(yMin, d).Length, SigFormat(yMax, d).Length);

			var frame = new FrameBuffer(width + xMargin, height);
			var yAxis = Axis.Linear(yMin, yMax, height);
			var xAxis = Axis.Linear(0, count - 1, width);

			frame.DrawXAxis(width, xMargin, end: '+');
			frame.DrawYAxis(height, xMargin, end: '+');
			frame.DrawXAxis(width, xMargin, height - 1, end: '+');
			frame.DrawYAxis(height, xMargin + width - 1, end: '+');

			if (width >= 10)
			{
				frame.DrawXGridLine(width, xMargin, 1 * height / 4, end: '+');
				frame.DrawXGridLine(width, xMargin, 3 * height / 4, end: '+');
			}
			if (height >= 10)
			{
				frame.DrawYGridLine(height, xMargin + (1 * width / 4), end: '+');
				frame.DrawYGridLine(height, xMargin + (3 * width / 4), end: '+');
				frame.DrawText(0, 1 * height / 4, SigFormat(yMin + 1 * (yMax - yMin) / 4, d), xMargin - 1);
				frame.DrawText(0, 3 * height / 4, SigFormat(yMin + 3 * (yMax - yMin) / 4, d), xMargin - 1);
			}
			frame.DrawXGridLine(width, xMargin, height / 2, end: '+');
			frame.DrawYGridLine(height, xMargin + (width / 2), end: '+');

			frame.DrawText(0, 0, SigFormat(yMin, d), xMargin - 1);
			frame.DrawText(0, height / 2, SigFormat((yMin + yMax) / 2, d), xMargin - 1);
			frame.DrawText(0, height - 1, SigFormat(yMax, d), xMargin - 1);

			chars ??= "#*%$ï¿½";
			for (int i = 0; i < series.Length; i++)
			{
				DrawData(frame, series[i], xAxis, yAxis, x0: xMargin, y0: 0, c: chars[i % chars.Length], nc: 'x');
			}
			return frame;
		}

		/// <summary>Render un XY Plot à partir d'une série de données (x, y)</summary>
		/// <param name="xs">Liste des coordonnées X des points</param>
		/// <param name="ys">Liste des coordonnées Y des points</param>
		/// <param name="width">Largeur (en caractères) de la zone de dessin (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="height">Hauteur (en caractères) de la frame (le buffer sera plus grand car incluant les axes, labels, ...)</param>
		/// <param name="xMin">Valeur mini sur l'axe X, ou calculé automatiquement si null</param>
		/// <param name="xMax">Valeur maxi sur l'axe X, ou calculé automatiquement si null</param>
		/// <param name="yMin">Valeur mini sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="yMax">Valeur maxi sur l'axe Y, ou calculé automatiquement si null</param>
		/// <param name="digits">Nombre de digits utilisé pour l'arrondi. Si positif, nombre de décimales. Si négatif, arrondi en millers/millions, etc.. (ex: 2 pour 1.2345=>'1.23', ou -3 pour 123456=>'123000'</param>
		/// <param name="c">Caractère utilisé pour le dessin des points</param>
		/// <returns>Buffer contenant le graph généré</returns>
		[Pure]
		public static FrameBuffer RenderFrame(Data xs, Data ys, int width, int height, double? xMin = null, double? xMax = null, double? yMin = null, double? yMax = null, int digits = 0, char c = '#')
		{
			var xMinActual = SigFloor(xMin ?? xs.Min() ?? 0, digits);
			var xMaxActual = SigCeiling(xMax ?? xs.Max() ?? 0, digits);
			var yMinActual = SigFloor(yMin ?? ys.Min() ?? 0, digits);
			var yMaxActual = SigCeiling(yMax ?? ys.Max() ?? 0, digits);

			int d = Math.Max(0, digits);
			int xMargin = 1 + Math.Max(SigFormat(yMinActual, d).Length, SigFormat(yMaxActual, d).Length);

			var frame = new FrameBuffer(width + xMargin, height);
			var yAxis = Axis.Linear(yMinActual, yMaxActual, height);
			var xAxis = Axis.Linear(xMinActual, xMaxActual, width);

			frame.DrawXAxis(width, xMargin, end: '+');
			frame.DrawYAxis(height, xMargin, end: '+');
			frame.DrawXAxis(width, xMargin, height - 1, end: '+');
			frame.DrawYAxis(height, xMargin + width - 1, end: '+');

			if (width >= 10)
			{
				frame.DrawXGridLine(width, xMargin, 1 * height / 4, end: '+');
				frame.DrawXGridLine(width, xMargin, 3 * height / 4, end: '+');
			}
			if (height >= 10)
			{
				frame.DrawYGridLine(height, xMargin + (1 * width / 4), end: '+');
				frame.DrawYGridLine(height, xMargin + (3 * width / 4), end: '+');
				frame.DrawText(0, 1 * height / 4, SigFormat(yMinActual + 1 * (yMaxActual - yMinActual) / 4, d), xMargin - 1);
				frame.DrawText(0, 3 * height / 4, SigFormat(yMinActual + 3 * (yMaxActual - yMinActual) / 4, d), xMargin - 1);
			}
			frame.DrawXGridLine(width, xMargin, height / 2, end: '+');
			frame.DrawYGridLine(height, xMargin + (width / 2), end: '+');

			frame.DrawText(0, 0, SigFormat(yMinActual, d), xMargin - 1);
			frame.DrawText(0, height / 2, SigFormat((yMinActual + yMaxActual) / 2, d), xMargin - 1);
			frame.DrawText(0, height - 1, SigFormat(yMaxActual, d), xMargin - 1);

			DrawData(frame, xs, ys, xAxis, yAxis, x0: xMargin, y0: 0, c: c, nc: 'x');
			return frame;
		}

	}

}
