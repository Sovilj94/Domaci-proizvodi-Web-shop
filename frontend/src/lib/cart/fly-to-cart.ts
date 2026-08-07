/** Leteća zelena tačka od izvornog dugmeta do elementa s id="cart-target" (ili "cart-target-mobile"). */
export function flyToCart(fromEl: HTMLElement) {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  const targets = [
    document.getElementById("cart-target"),
    document.getElementById("cart-target-mobile"),
  ].filter((el): el is HTMLElement => el !== null);
  if (targets.length === 0) return;
  const flyTarget = targets[0];
  const a = fromEl.getBoundingClientRect();
  const b = flyTarget.getBoundingClientRect();
  const dot = document.createElement("span");
  dot.style.cssText = `position:fixed;left:${a.left + a.width / 2}px;top:${a.top + a.height / 2}px;width:14px;height:14px;border-radius:9999px;background:#2a7a3a;z-index:60;pointer-events:none;transform:translate(-50%,-50%)`;
  document.body.appendChild(dot);
  const dx = b.left + b.width / 2 - (a.left + a.width / 2);
  const dy = b.top + b.height / 2 - (a.top + a.height / 2);
  dot.animate(
    [
      { transform: "translate(-50%,-50%) scale(1)", opacity: 1 },
      { transform: `translate(calc(-50% + ${dx}px), calc(-50% + ${dy}px)) scale(0.3)`, opacity: 0.4 },
    ],
    { duration: 600, easing: "cubic-bezier(0.5,0,0.75,0)" },
  ).onfinish = () => {
    dot.remove();
    for (const t of targets) {
      t.classList.add("animate-bump");
      setTimeout(() => t.classList.remove("animate-bump"), 400);
    }
  };
}
