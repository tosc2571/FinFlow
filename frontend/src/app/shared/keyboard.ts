/**
 * Enter anywhere in a toolbar/form triggers its primary action — used instead of relying on
 * native HTML form submission because that doesn't fire on Enter from a <select> (only text
 * inputs reliably trigger it; dropdowns just consume the keypress). Skips the case where focus
 * is already on a <button>: that button already handles Enter as its own native click, so
 * calling the action again here would double-fire it (e.g. pressing Enter on a focused
 * "Cancel" button must not also trigger "Save").
 */
export function onEnterSubmit(event: Event, action: () => void): void {
  if ((event.target as HTMLElement).tagName === 'BUTTON') return;
  event.preventDefault();
  action();
}
