/**
 * Enter anywhere in a toolbar/form triggers its primary action — used instead of relying on
 * native HTML form submission because that doesn't fire on Enter from a <select> (only text
 * inputs reliably trigger it; dropdowns just consume the keypress).
 *
 * Bind this to (keyup.enter), not (keydown.enter): while a native <select>'s dropdown popup is
 * open, the browser can swallow the Enter *keydown* internally to close the popup, so it never
 * bubbles up to a parent listener — but the *keyup* that follows once the key is released fires
 * normally after the popup has already closed, by which point ngModel has also already picked
 * up the new value (its own change/input listener runs before keyup). Works identically for
 * plain text inputs either way, so one binding covers both.
 *
 * Skips the case where focus is already on a <button>: that button already handles Enter as its
 * own native click, so calling the action again here would double-fire it (e.g. pressing Enter
 * on a focused "Cancel" button must not also trigger "Save").
 */
export function onEnterSubmit(event: Event, action: () => void): void {
  if ((event.target as HTMLElement).tagName === 'BUTTON') return;
  event.preventDefault();
  action();
}
