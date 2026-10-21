/**
 * A small PNG reader.
 *
 * The installer paints the icon of the application in its own artwork, and
 * the icon comes as a PNG. Only the kind the application ships is handled:
 * 8 bits per channel, with or without an alpha channel.
 */
import { inflateSync } from "node:zlib";

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const CHANNELS = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 };

/**
 * Reads a PNG and returns the pixels as red, green, blue and alpha, row by
 * row, from the top down.
 */
export const decodePng = (buffer) => {
  if (!buffer.subarray(0, 8).equals(SIGNATURE)) {
    throw new Error("O arquivo nao e um PNG");
  }

  let offset = 8;
  let header = null;
  let palette = null;
  let transparency = null;
  const data = [];

  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString("ascii", offset + 4, offset + 8);
    const body = buffer.subarray(offset + 8, offset + 8 + length);
    offset += 12 + length;

    if (type === "IHDR") {
      header = {
        width: body.readUInt32BE(0),
        height: body.readUInt32BE(4),
        depth: body[8],
        color: body[9],
        interlace: body[12],
      };
    } else if (type === "PLTE") {
      palette = body;
    } else if (type === "tRNS") {
      transparency = body;
    } else if (type === "IDAT") {
      data.push(body);
    } else if (type === "IEND") {
      break;
    }
  }

  if (!header) throw new Error("O PNG nao tem cabecalho");
  if (header.depth !== 8) throw new Error("O PNG precisa ter 8 bits por canal");
  if (header.interlace !== 0) throw new Error("PNG entrelacado nao e suportado");

  const channels = CHANNELS[header.color];
  if (!channels) throw new Error("O tipo de cor do PNG nao e suportado");

  const raw = inflateSync(Buffer.concat(data));
  const { width, height } = header;
  const stride = width * channels;
  const pixels = Buffer.alloc(width * height * 4);

  // Cada linha do PNG e filtrada, o filtro precisa ser desfeito.
  const line = Buffer.alloc(stride);
  let previous = Buffer.alloc(stride);
  let at = 0;

  for (let row = 0; row < height; row += 1) {
    const filter = raw[at];
    at += 1;
    raw.copy(line, 0, at, at + stride);
    at += stride;

    for (let index = 0; index < stride; index += 1) {
      const left = index >= channels ? line[index - channels] : 0;
      const up = previous[index];
      const corner = index >= channels ? previous[index - channels] : 0;

      switch (filter) {
        case 0:
          break;
        case 1:
          line[index] = (line[index] + left) & 0xff;
          break;
        case 2:
          line[index] = (line[index] + up) & 0xff;
          break;
        case 3:
          line[index] = (line[index] + ((left + up) >> 1)) & 0xff;
          break;
        case 4: {
          const estimate = left + up - corner;
          const distanceLeft = Math.abs(estimate - left);
          const distanceUp = Math.abs(estimate - up);
          const distanceCorner = Math.abs(estimate - corner);
          const nearest =
            distanceLeft <= distanceUp && distanceLeft <= distanceCorner
              ? left
              : distanceUp <= distanceCorner
                ? up
                : corner;
          line[index] = (line[index] + nearest) & 0xff;
          break;
        }
        default:
          throw new Error(`Filtro de PNG desconhecido: ${filter}`);
      }
    }

    for (let column = 0; column < width; column += 1) {
      const source = column * channels;
      const target = (row * width + column) * 4;

      if (header.color === 6) {
        pixels[target] = line[source];
        pixels[target + 1] = line[source + 1];
        pixels[target + 2] = line[source + 2];
        pixels[target + 3] = line[source + 3];
      } else if (header.color === 2) {
        pixels[target] = line[source];
        pixels[target + 1] = line[source + 1];
        pixels[target + 2] = line[source + 2];
        pixels[target + 3] = 255;
      } else if (header.color === 0) {
        pixels[target] = line[source];
        pixels[target + 1] = line[source];
        pixels[target + 2] = line[source];
        pixels[target + 3] = 255;
      } else if (header.color === 4) {
        pixels[target] = line[source];
        pixels[target + 1] = line[source];
        pixels[target + 2] = line[source];
        pixels[target + 3] = line[source + 1];
      } else if (header.color === 3) {
        const index = line[source] * 3;
        pixels[target] = palette[index];
        pixels[target + 1] = palette[index + 1];
        pixels[target + 2] = palette[index + 2];
        pixels[target + 3] =
          transparency && line[source] < transparency.length
            ? transparency[line[source]]
            : 255;
      }
    }

    previous = Buffer.from(line);
  }

  return { width, height, pixels };
};
