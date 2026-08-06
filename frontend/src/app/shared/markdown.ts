import { Marked } from 'marked';

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/**
 * A marked instance with one addition: ```mermaid fenced blocks render as `<pre class="mermaid">`
 * instead of a plain code block, so callers can hand those elements to mermaid.js for diagram
 * rendering once they're in the DOM. Everything else falls through to marked's normal rendering
 * (returning `false` from a renderer override is marked's documented "use the default" signal).
 */
const markdown = new Marked({
  renderer: {
    code({ text, lang }) {
      if (lang !== 'mermaid') return false;
      return `<pre class="mermaid">${escapeHtml(text)}</pre>`;
    },
  },
});

export function renderMarkdown(content: string): string {
  return markdown.parse(content, { async: false }) as string;
}
