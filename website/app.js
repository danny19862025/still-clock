(() => {
  "use strict";
  const REPO = "danny19862025/still-clock";
  const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const pad = n => String(n).padStart(2, "0");
  const body = document.body;

  /* ---------- Time helpers ---------- */
  const WD = ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"];
  const MO = ["JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"];
  const tzLabel = () => {
    const off = -new Date().getTimezoneOffset();
    const s = off >= 0 ? "+" : "-", a = Math.abs(off);
    return `UTC${s}${pad(Math.floor(a / 60))}:${pad(a % 60)}`;
  };

  /* ---------- Intro reveal ---------- */
  const intro = $("#intro");
  const introWord = $(".intro-word");
  introWord.innerHTML = [...introWord.textContent].map((c, i) => `<span class="ch" style="animation-delay:${i * 90}ms">${c}</span>`).join("");
  const introClock = $("#introClock"), introBar = $("#introBar"), introStatus = $("#introStatus");
  const statuses = ["SYNCING CHRONO CORE", "LOADING PRESETS", "CALIBRATING GLOW", "READY"];
  let revealed = false;

  function revealSite() {
    if (revealed) return;
    revealed = true;
    intro.classList.add("done");
    body.classList.remove("is-loading");
    setTimeout(() => body.classList.add("revealed"), 250);
    setTimeout(() => intro.classList.add("gone"), 1600);
  }

  const skipIntro = reduced || /[?&]skip/.test(location.search) || sessionStorage.getItem("still-intro") === "1";
  try { sessionStorage.setItem("still-intro", "1"); } catch {}
  if (skipIntro) {
    intro.classList.add("gone");
    revealSite();
  } else {
    const DURATION = 2000, start = performance.now();
    const glyphs = "0123456789";
    const frame = now => {
      if (revealed) return;
      const t = Math.min(1, (now - start) / DURATION);
      const eased = 1 - Math.pow(1 - t, 3);
      introBar.style.width = (eased * 100).toFixed(1) + "%";
      introStatus.textContent = statuses[Math.min(statuses.length - 1, Math.floor(t * statuses.length))];
      // Scramble digits that settle to the real time from left to right
      const d = new Date();
      const real = `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
      const settled = Math.floor(eased * 1.25 * real.length);
      introClock.textContent = [...real].map((c, i) => (c === ":" || i < settled) ? c : glyphs[(Math.random() * 10) | 0]).join("");
      if (t < 1) requestAnimationFrame(frame);
      else setTimeout(revealSite, 350);
    };
    requestAnimationFrame(frame);
    intro.style.pointerEvents = "auto";
    intro.addEventListener("click", revealSite);
    addEventListener("keydown", e => { if (e.key === "Escape") revealSite(); }, { once: true });
  }

  /* ---------- Split hero headline ---------- */
  const h1 = $("h1");
  let wi = 0;
  const wrap = html => `<span class="split-word"><span style="--i:${wi++}">${html}</span></span>`;
  h1.innerHTML = [...h1.childNodes].map(n => {
    if (n.nodeType === 3) return n.textContent.split(/(\s+)/).map(w => w.trim() ? wrap(w) : w).join("");
    return wrap(n.outerHTML);
  }).join("");

  /* ---------- Live clocks ---------- */
  const set = (sel, v) => $$(sel).forEach(el => { if (el.textContent !== v) el.textContent = v; });
  const flipTiles = $$(".tile[data-d]");
  function tick() {
    const d = new Date();
    const h = d.getHours(), m = d.getMinutes(), s = d.getSeconds();
    const H = pad(h), M = pad(m), S = pad(s);
    const wd = WD[d.getDay()], mo = MO[d.getMonth()];
    set("#navTime", `${H}:${M}:${S}`);
    set("#navTz", tzLabel());
    set("[data-h]", H); set("[data-m]", M); set("[data-s]", S);
    set("[data-wd-short]", wd.slice(0, 3));
    set("[data-wd-long]", wd);
    set("[data-iso]", `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`);
    set("[data-dm]", `${pad(d.getDate())} ${mo.slice(0, 3)}`);
    set("[data-long-date]", `${mo} ${pad(d.getDate())}, ${d.getFullYear()}`);
    set("[data-hm12]", `${(h % 12) || 12}:${M} ${h < 12 ? "AM" : "PM"}`);
    set("[data-hms]", `${H}:${M}:${S}`);
    set("[data-term-date]", `${wd.toLowerCase()} ${pad(d.getDate())} ${mo.toLowerCase()} ${d.getFullYear()}`);
    const fill = $("[data-progress]");
    if (fill) fill.style.width = ((s + d.getMilliseconds() / 1000) / 60 * 100).toFixed(2) + "%";
    const digits = H + M;
    flipTiles.forEach((t, i) => {
      if (t.textContent !== digits[i]) {
        t.textContent = digits[i];
        if (!reduced) { t.classList.remove("flip"); void t.offsetWidth; t.classList.add("flip"); }
      }
    });
  }
  tick();
  setInterval(tick, 250);

  /* ---------- Segmented controls with sliding pill ---------- */
  function initSeg(seg, onSelect) {
    const pill = document.createElement("span");
    pill.className = "seg-pill";
    seg.prepend(pill);
    const buttons = $$("button", seg);
    const move = btn => { pill.style.left = btn.offsetLeft + "px"; pill.style.width = btn.offsetWidth + "px"; };
    const select = (btn, user) => {
      buttons.forEach(b => b.setAttribute("aria-selected", String(b === btn)));
      move(btn);
      onSelect(btn, user);
    };
    buttons.forEach(b => b.addEventListener("click", () => select(b, true)));
    const sync = () => move(buttons.find(b => b.getAttribute("aria-selected") === "true") || buttons[0]);
    addEventListener("resize", sync);
    document.fonts && document.fonts.ready.then(sync);
    requestAnimationFrame(sync);
    return { select, buttons };
  }

  const faceMeta = {
    neon: ["NEON", "TRANSPARENT", "FX GLOW"],
    flip: ["FLIP", "SOLID", "FX TILES"],
    spaced: ["SPACED", "TRANSPARENT", "FX SOFT GLOW"],
    terminal: ["TERMINAL", "CRT PANEL", "FX SCANLINES"],
  };
  let autoCycle = true;
  const faceSeg = initSeg($(".preview .seg"), (btn, user) => {
    if (user) autoCycle = false;
    const f = btn.dataset.face;
    $$(".face").forEach(el => el.classList.toggle("active", el.dataset.face === f));
    const [n, s, fx] = faceMeta[f];
    $("#faceName").textContent = n; $("#faceSurface").textContent = s; $("#faceFx").textContent = fx;
  });
  if (!reduced) {
    setInterval(() => {
      if (!autoCycle || !revealed || document.hidden) return;
      const b = faceSeg.buttons, i = b.findIndex(x => x.getAttribute("aria-selected") === "true");
      faceSeg.select(b[(i + 1) % b.length], false);
    }, 5500);
  }

  initSeg($(".install .seg"), btn => {
    $$(".os-panel").forEach(p => p.classList.toggle("active", p.dataset.os === btn.dataset.os));
  });

  /* ---------- Gallery ---------- */
  const presets = [
    ["neon", "Neon", "Thin glowing digits, a sweeping seconds bar, a blinking colon and an ISO date. The default."],
    ["flip", "Flip", "A charcoal split-flap clock. Each digit on its own tile with hinge tabs."],
    ["flip-light", "Flip Light", "The split-flap layout in warm white."],
    ["spaced", "Spaced", "A wide, letter-spaced weekday, full date and time between two rules with a soft pink glow."],
    ["poster", "Poster", "Editorial: a huge fading day number with the bold month running across it."],
    ["signature", "Signature", "A tall day number with a handwritten blue weekday sweeping across."],
    ["midnight", "Midnight", "A big see-through condensed time with a white handwritten weekday."],
    ["arc", "Arc", "Hours stacked over fading minutes under a glowing teal arc."],
    ["split", "Split", "A flat translucent panel with a bold hot-pink hour and raised seconds."],
    ["neon-poster", "Neon Poster", "The Poster layout in hot magenta with glow and a seconds bar."],
    ["hud", "HUD", "A scanlined instrument panel with corner brackets and segmented seconds."],
    ["holo", "Holo", "Magenta-to-cyan gradient digits in an outlined glass card."],
    ["terminal", "Terminal", "Glowing green monospace on a CRT panel with a > now prompt."],
  ];
  const gallery = $("#gallery");
  gallery.innerHTML = presets.map(([id, name, desc], i) => `
    <figure class="card reveal reveal-scale" style="--d:${(i % 3) * 110}ms" tabindex="0" data-img="images/preset-${id}.png" data-name="${name}">
      <div class="card-img"><img src="images/preset-${id}.png" alt="${name} preset" loading="lazy"></div>
      <figcaption class="card-body"><div class="card-name">${name.toUpperCase()}</div><div class="card-desc">${desc}</div></figcaption>
      <span class="shine"></span>
    </figure>`).join("");

  const lb = $("#lightbox");
  const openLb = card => {
    $("#lbImg").src = card.dataset.img; $("#lbImg").alt = card.dataset.name + " preset";
    $("#lbCap").textContent = card.dataset.name.toUpperCase();
    lb.showModal();
  };
  gallery.addEventListener("click", e => { const c = e.target.closest(".card"); if (c) openLb(c); });
  gallery.addEventListener("keydown", e => { const c = e.target.closest(".card"); if (c && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); openLb(c); } });
  $("#lbClose").addEventListener("click", () => lb.close());
  lb.addEventListener("click", e => { if (e.target === lb) lb.close(); });

  /* ---------- Scroll reveal ---------- */
  const tag = (sel, cls = "", step = 0) => $$(sel).forEach((el, i) => {
    el.classList.add("reveal");
    if (cls) el.classList.add(cls);
    if (step) el.style.setProperty("--d", `${i * step}ms`);
  });
  $$(".section-head").forEach(head => [...head.children].forEach((el, i) => { el.classList.add("reveal"); el.style.setProperty("--d", `${i * 120}ms`); }));
  $$(".features .feature").forEach((el, i) => { el.classList.add("reveal"); el.style.setProperty("--d", `${(i % 3) * 120 + Math.floor(i / 3) * 80}ms`); });
  $$(".principles .principle").forEach((el, i) => { el.classList.add("reveal", "reveal-scale"); el.style.setProperty("--d", `${i * 80}ms`); });
  tag(".stat", "", 120);
  tag(".shot", "reveal-clip");
  tag(".install", "reveal-scale");
  tag(".steps li", "reveal-left", 120);
  tag(".support-card", "reveal-scale");
  tag(".fine");
  tag(".footer");

  const io = new IntersectionObserver(entries => {
    entries.forEach(en => {
      if (!en.isIntersecting) return;
      en.target.classList.add("in");
      io.unobserve(en.target);
      const delay = parseInt(en.target.style.getPropertyValue("--d")) || 0;
      setTimeout(() => en.target.classList.add("settled"), 1500 + delay);
      if (en.target.classList.contains("stat")) countUp($(".stat-n", en.target));
    });
  }, { threshold: 0.15, rootMargin: "0px 0px -8% 0px" });
  $$(".reveal").forEach(el => io.observe(el));

  function countUp(el) {
    const target = +el.dataset.count;
    if (reduced || target === 0) { el.textContent = target; return; }
    const dur = 1400, t0 = performance.now();
    const step = now => {
      const t = Math.min(1, (now - t0) / dur);
      el.textContent = Math.round(target * (1 - Math.pow(1 - t, 4)));
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  /* ---------- Scroll effects: progress, nav, active link, parallax ---------- */
  const progress = $("#progress"), nav = $(".nav"), heroCopy = $(".hero-copy");
  const links = $$(".nav-links a[href^='#']");
  const sections = links.map(a => $(a.getAttribute("href")));
  let ticking = false;
  function onScroll() {
    const y = scrollY, max = document.documentElement.scrollHeight - innerHeight;
    progress.style.transform = `scaleX(${max > 0 ? y / max : 0})`;
    nav.classList.toggle("scrolled", y > 10);
    if (!reduced && y < innerHeight * 1.2) {
      heroCopy.style.transform = `translateY(${y * 0.18}px)`;
      heroCopy.style.opacity = String(Math.max(0, 1 - y / (innerHeight * 0.9)));
    }
    let current = -1;
    sections.forEach((s, i) => { if (s && s.getBoundingClientRect().top < innerHeight * 0.4) current = i; });
    links.forEach((a, i) => a.classList.toggle("active", i === current));
    ticking = false;
  }
  addEventListener("scroll", () => { if (!ticking) { ticking = true; requestAnimationFrame(onScroll); } }, { passive: true });
  onScroll();

  /* ---------- Pointer effects ---------- */
  const fine = matchMedia("(pointer: fine)").matches;
  if (fine && !reduced) {
    const root = document.documentElement;
    addEventListener("pointermove", e => {
      root.style.setProperty("--mx", e.clientX + "px");
      root.style.setProperty("--my", e.clientY + "px");
    }, { passive: true });

    // 3D tilt on the hero preview
    const preview = $(".preview");
    preview.addEventListener("pointermove", e => {
      const r = preview.getBoundingClientRect();
      const x = (e.clientX - r.left) / r.width - .5, y = (e.clientY - r.top) / r.height - .5;
      preview.classList.add("tilting");
      preview.style.setProperty("--ry", `${x * 10}deg`);
      preview.style.setProperty("--rx", `${-y * 10}deg`);
    });
    preview.addEventListener("pointerleave", () => {
      preview.classList.remove("tilting");
      preview.style.setProperty("--ry", "0deg"); preview.style.setProperty("--rx", "0deg");
    });

    // Tilt + spotlight on cards and features
    document.addEventListener("pointermove", e => {
      const el = e.target.closest(".card, .feature");
      if (!el) return;
      const r = el.getBoundingClientRect();
      const px = e.clientX - r.left, py = e.clientY - r.top;
      el.style.setProperty("--px", px + "px"); el.style.setProperty("--py", py + "px");
      if (el.classList.contains("card") && el.classList.contains("in")) {
        const x = px / r.width - .5, y = py / r.height - .5;
        el.style.transform = `perspective(900px) rotateY(${x * 9}deg) rotateX(${-y * 9}deg) translateZ(0)`;
      }
    }, { passive: true });
    document.addEventListener("pointerout", e => {
      const el = e.target.closest(".card");
      if (el && !el.contains(e.relatedTarget)) el.style.transform = "";
    });

    // Magnetic buttons
    $$(".btn").forEach(btn => {
      btn.addEventListener("pointermove", e => {
        const r = btn.getBoundingClientRect();
        btn.style.transform = `translate(${(e.clientX - r.left - r.width / 2) * .22}px, ${(e.clientY - r.top - r.height / 2) * .3}px)`;
      });
      btn.addEventListener("pointerleave", () => { btn.style.transform = ""; });
    });
  }

  /* ---------- Particle field ---------- */
  const canvas = $("#particles");
  if (!reduced && canvas.getContext) {
    const ctx = canvas.getContext("2d");
    let W, H, dpr, pts = [], mouse = { x: -9999, y: -9999 };
    const resize = () => {
      dpr = Math.min(2, devicePixelRatio || 1);
      W = canvas.width = innerWidth * dpr; H = canvas.height = innerHeight * dpr;
      const n = Math.min(90, Math.floor(innerWidth * innerHeight / 18000));
      pts = Array.from({ length: n }, () => ({
        x: Math.random() * W, y: Math.random() * H,
        vx: (Math.random() - .5) * .25 * dpr, vy: (Math.random() - .5) * .25 * dpr,
        r: (Math.random() * 1.4 + .4) * dpr, hue: Math.random() < .75 ? "34,229,255" : "255,43,214",
      }));
    };
    addEventListener("resize", resize);
    addEventListener("pointermove", e => { mouse.x = e.clientX * dpr; mouse.y = e.clientY * dpr; }, { passive: true });
    resize();
    const LINK = 130;
    const draw = () => {
      if (!document.hidden) {
        ctx.clearRect(0, 0, W, H);
        for (const p of pts) {
          const dx = p.x - mouse.x, dy = p.y - mouse.y, d2 = dx * dx + dy * dy, R = 140 * dpr;
          if (d2 < R * R) { const f = (1 - Math.sqrt(d2) / R) * .6; p.x += dx / R * f * 4; p.y += dy / R * f * 4; }
          p.x += p.vx; p.y += p.vy;
          if (p.x < 0) p.x = W; if (p.x > W) p.x = 0; if (p.y < 0) p.y = H; if (p.y > H) p.y = 0;
          ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
          ctx.fillStyle = `rgba(${p.hue},.7)`; ctx.fill();
        }
        const L = LINK * dpr;
        for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) {
          const a = pts[i], b = pts[j], dx = a.x - b.x, dy = a.y - b.y, d = Math.hypot(dx, dy);
          if (d < L) {
            ctx.strokeStyle = `rgba(34,229,255,${(1 - d / L) * .16})`;
            ctx.lineWidth = dpr * .6;
            ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
          }
        }
      }
      requestAnimationFrame(draw);
    };
    draw();
  }

  /* ---------- Marquee: duplicate content for seamless loop ---------- */
  const track = $("#stripTrack");
  track.innerHTML += track.innerHTML;
  [...track.children].slice(track.children.length / 2).forEach(el => el.setAttribute("aria-hidden", "true"));

  /* ---------- OS-aware downloads ---------- */
  const base = `https://github.com/${REPO}/releases/latest/download/`;
  const ua = navigator.userAgent;
  if (/Linux/i.test(ua) && !/Android/i.test(ua)) {
    $("#primaryDownload").href = base + "Still-linux-x64.tar.gz";
    $("#primaryLabel").textContent = "Download for Linux";
    $("#secondaryDownload").href = base + "Still-Setup-win-x64.exe";
    $("#secondaryLabel").textContent = "Download for Windows";
    const linuxTab = $(".install .seg button[data-os='linux']");
    linuxTab && linuxTab.click();
  }

  /* ---------- Latest version from GitHub ---------- */
  fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: "application/vnd.github+json" } })
    .then(r => r.ok ? r.json() : null)
    .then(j => { if (j && j.tag_name) $("#versionTag").textContent = "Still " + j.tag_name.replace(/^v/, ""); })
    .catch(() => {});

  /* ---------- Copy buttons ---------- */
  $$(".copy").forEach(b => b.addEventListener("click", async () => {
    try { await navigator.clipboard.writeText(b.dataset.copy); b.textContent = "COPIED ✓"; }
    catch { b.textContent = "SELECT & COPY"; }
    setTimeout(() => { b.textContent = "COPY"; }, 1600);
  }));
})();
