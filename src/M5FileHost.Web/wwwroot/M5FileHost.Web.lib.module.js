export function afterWebStarted(blazor) {
  blazor.addEventListener('enhancedload', () => {
    document.dispatchEvent(new CustomEvent('m5:enhance'));
  });
}
