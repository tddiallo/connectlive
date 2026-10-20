// Petites fonctions de dessin SVG partagées par les générateurs de schémas (voir docs/img/src/README.md).
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));

/** Logo officiel (Simple Icons, CC0) encodé en data-URI pour que le SVG reste autonome. */
export function iconDataUri(name) {
  const svg = readFileSync(join(here, 'icons', `${name}.svg`));
  return `data:image/svg+xml;base64,${svg.toString('base64')}`;
}

export const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

export const FONT = "Segoe UI, Helvetica, Arial, sans-serif";
export const MONO = "Consolas, Menlo, monospace";

export function rect(x, y, w, h, { fill = '#fff', stroke = 'none', sw = 2, r = 16, dash } = {}) {
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"${dash ? ` stroke-dasharray="${dash}"` : ''}/>`;
}

export function text(x, y, s, { size = 20, fill = '#fff', weight = 400, anchor = 'start', mono = false, opacity } = {}) {
  return `<text x="${x}" y="${y}" font-size="${size}" fill="${fill}" font-weight="${weight}" text-anchor="${anchor}" font-family="${mono ? MONO : FONT}"${opacity ? ` opacity="${opacity}"` : ''}>${esc(s)}</text>`;
}

/** Tuile blanche arrondie portant un logo : lisible sur fond sombre comme sur fond clair. */
export function logoTile(x, y, size, icon, { r = 18 } = {}) {
  const pad = size * 0.2;
  return rect(x, y, size, size, { fill: '#ffffff', r }) +
    `<image href="${iconDataUri(icon)}" x="${x + pad}" y="${y + pad}" width="${size - 2 * pad}" height="${size - 2 * pad}"/>`;
}

export function arrow(x1, y1, x2, y2, { color = '#94a3b8', width = 3, dash, id = 'arr' } = {}) {
  return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${color}" stroke-width="${width}"${dash ? ` stroke-dasharray="${dash}"` : ''} marker-end="url(#${id})"/>`;
}

export function path(d, { color = '#94a3b8', width = 3, dash, id = 'arr' } = {}) {
  return `<path d="${d}" fill="none" stroke="${color}" stroke-width="${width}"${dash ? ` stroke-dasharray="${dash}"` : ''} marker-end="url(#${id})"/>`;
}

export function markers(colors) {
  return `<defs>${Object.entries(colors).map(([id, c]) =>
    `<marker id="${id}" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${c}"/></marker>`).join('')}</defs>`;
}

export function svgDocument(w, h, body, extraDefs = '') {
  return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" font-family="${FONT}">${extraDefs}${body}</svg>\n`;
}
