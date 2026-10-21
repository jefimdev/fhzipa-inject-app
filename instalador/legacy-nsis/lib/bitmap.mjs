/**
 * A tiny bitmap writer.
 *
 * The installer draws its own background instead of using the stock wizard
 * art, so the whole thing carries the colours of the application. The output
 * is a plain 24 bit BMP, the format the Windows controls understand.
 */
import { writeFileSync } from "node:fs";

export class Bitmap {
  constructor(width, height, background = [0, 0, 0]) {
    this.width = width;
    this.height = height;
    this.data = Buffer.alloc(width * height * 3);
    this.fill(0, 0, width, height, background);
  }

  /** Paints a rectangle. */
  fill(x, y, w, h, color) {
    const [r, g, b] = color;
    const from = Math.max(0, Math.round(x));
    const to = Math.min(this.width, Math.round(x + w));
    const top = Math.max(0, Math.round(y));
    const bottom = Math.min(this.height, Math.round(y + h));

    for (let row = top; row < bottom; row += 1) {
      for (let column = from; column < to; column += 1) {
        const at = (row * this.width + column) * 3;
        this.data[at] = b;
        this.data[at + 1] = g;
        this.data[at + 2] = r;
      }
    }
  }

  /** Paints a rectangle with rounded corners. */
  roundRect(x, y, w, h, radius, color) {
    const limit = Math.min(radius, Math.floor(w / 2), Math.floor(h / 2));
    for (let row = 0; row < h; row += 1) {
      for (let column = 0; column < w; column += 1) {
        const dx = column < limit ? limit - column : column >= w - limit ? column - (w - limit - 1) : 0;
        const dy = row < limit ? limit - row : row >= h - limit ? row - (h - limit - 1) : 0;
        if (dx * dx + dy * dy <= limit * limit + limit) {
          this.fill(x + column, y + row, 1, 1, color);
        }
      }
    }
  }

  /** Paints a rectangle with a border. */
  outlineRoundRect(x, y, w, h, radius, color, thickness = 1) {
    for (let step = 0; step < thickness; step += 1) {
      this.roundRect(x + step, y + step, w - step * 2, h - step * 2, radius - step, color);
      // The outline is a ring, the middle is painted over by the caller.
      this.fill(x + step + radius, y + step, w - step * 2 - radius * 2, 1, color);
      this.fill(x + step + radius, y + h - step - 1, w - step * 2 - radius * 2, 1, color);
      this.fill(x + step, y + step + radius, 1, h - step * 2 - radius * 2, color);
      this.fill(x + w - step - 1, y + step + radius, 1, h - step * 2 - radius * 2, color);
    }
  }

  /** A vertical gradient, from one colour to another. */
  gradientV(x, y, w, h, from, to) {
    for (let row = 0; row < h; row += 1) {
      const weight = h <= 1 ? 0 : row / (h - 1);
      const color = [
        Math.round(from[0] + (to[0] - from[0]) * weight),
        Math.round(from[1] + (to[1] - from[1]) * weight),
        Math.round(from[2] + (to[2] - from[2]) * weight),
      ];
      this.fill(x, y + row, w, 1, color);
    }
  }

  /** A soft round glow, the red halo of the application. */
  glow(cx, cy, radius, color, strength = 1) {
    for (let row = -radius; row <= radius; row += 1) {
      for (let column = -radius; column <= radius; column += 1) {
        const distance = Math.sqrt(row * row + column * column);
        if (distance > radius) continue;
        const falloff = (1 - distance / radius) ** 2 * strength;
        const at = ((cy + row) * this.width + (cx + column)) * 3;
        if (at < 0 || at + 2 >= this.data.length) continue;
        this.data[at] = Math.min(255, this.data[at] + color[2] * falloff);
        this.data[at + 1] = Math.min(255, this.data[at + 1] + color[1] * falloff);
        this.data[at + 2] = Math.min(255, this.data[at + 2] + color[0] * falloff);
      }
    }
  }

  /** Paints an image with transparency over the surface. */
  drawImage(image, x, y, width, height) {
    const source = image.width / image.height;
    const target = width / height;
    const cropX = source > target ? (1 - target / source) / 2 : 0;
    const cropY = target > source ? (1 - source / target) / 2 : 0;

    for (let row = 0; row < height; row += 1) {
      for (let column = 0; column < width; column += 1) {
        const u = cropX + (column / width) * (1 - cropX * 2);
        const v = cropY + (row / height) * (1 - cropY * 2);
        const from = (
          Math.min(image.height - 1, Math.max(0, Math.floor(v * image.height))) *
            image.width +
          Math.min(image.width - 1, Math.max(0, Math.floor(u * image.width)))
        ) * 4;
        const alpha = image.pixels[from + 3] / 255;
        if (alpha <= 0) continue;

        const at = (Math.round(y + row) * this.width + Math.round(x + column)) * 3;
        if (at < 0 || at + 2 >= this.data.length) continue;

        // O arquivo e na ordem inversa, azul primeiro.
        const source = [
          image.pixels[from + 2],
          image.pixels[from + 1],
          image.pixels[from],
        ];
        for (let channel = 0; channel < 3; channel += 1) {
          this.data[at + channel] = Math.round(
            source[channel] * alpha + this.data[at + channel] * (1 - alpha),
          );
        }
      }
    }
  }

  /** Writes the file, the rows go bottom up as the format asks. */
  write(path) {
    const rowSize = Math.ceil((this.width * 3) / 4) * 4;
    const padding = rowSize - this.width * 3;
    const pixels = Buffer.alloc(rowSize * this.height);

    for (let row = 0; row < this.height; row += 1) {
      const source = (this.height - 1 - row) * this.width * 3;
      this.data.copy(pixels, row * rowSize, source, source + this.width * 3);
    }

    const header = Buffer.alloc(54);
    const size = 54 + pixels.length;

    header.write("BM", 0, "ascii");
    header.writeUInt32LE(size, 2);
    header.writeUInt32LE(54, 10);
    header.writeUInt32LE(40, 14);
    header.writeInt32LE(this.width, 18);
    header.writeInt32LE(this.height, 22);
    header.writeUInt16LE(1, 26);
    header.writeUInt16LE(24, 28);
    header.writeUInt32LE(pixels.length, 34);

    writeFileSync(path, Buffer.concat([header, pixels]));
  }
}
