#include "web_page.h"

const char paginaHTML[] PROGMEM = R"rawliteral(
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#0c0f0d">
  <link rel="icon" href="data:,">
  <title>Control Autito</title>
  <style>
    :root {
      color-scheme: dark;
      --ink: #f0f1e8;
      --muted: #9aa196;
      --panel: #151916;
      --line: #343b34;
      --line-bright: #535d49;
      --accent: #d7ed38;
      --accent-glow: rgba(215, 237, 56, .16);
      --green: #b9e53b;
      --red: #f05239;
      --red-glow: rgba(240, 82, 57, .22);
      --font-display: Impact, "Arial Narrow", "Trebuchet MS", sans-serif;
      --font-sans: "Arial Narrow", "Trebuchet MS", system-ui, -apple-system, sans-serif;
      --font-mono: ui-monospace, "Cascadia Code", Consolas, monospace;
    }

    *, *::before, *::after { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
    [hidden], .hidden { display: none !important; }

    body {
      min-height: 100vh;
      margin: 0;
      color: var(--ink);
      font-family: var(--font-sans);
      background:
        repeating-linear-gradient(132deg, transparent 0 64px, rgba(255, 255, 255, .016) 65px 66px, transparent 67px 130px),
        radial-gradient(circle at 85% 8%, rgba(215, 237, 56, .04), transparent 42%),
        linear-gradient(135deg, #0a0d0b 0%, #111512 58%, #161712 100%);
      overflow-x: hidden;
    }

    main { width: min(100% - 24px, 840px); margin: 0 auto; padding: 24px 0 44px; }

    /* Cabecera */
    header {
      position: relative;
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 14px;
      padding-bottom: 16px;
      border-bottom: 3px solid var(--accent);
    }

    header::after { content: ""; position: absolute; right: 0; bottom: -3px; width: 68px; height: 3px; background: var(--red); }

    .eyebrow {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin: 0 0 4px;
      color: var(--accent);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: .12em;
      text-transform: uppercase;
    }

    .eyebrow-tag {
      padding: 1px 6px;
      border: 1px solid rgba(215, 237, 56, .4);
      background: rgba(215, 237, 56, .14);
      font: 10px var(--font-mono);
      letter-spacing: 0;
    }

    h1 {
      margin: 0;
      font-family: var(--font-display);
      font-size: clamp(38px, 7vw, 48px);
      font-weight: 400;
      line-height: .92;
      letter-spacing: .02em;
      text-transform: uppercase;
    }

    .status {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      min-height: 38px;
      padding: 6px 12px;
      border: 1px solid var(--line);
      background: #121613;
      color: var(--muted);
      font-size: 13px;
      font-weight: 700;
      letter-spacing: .03em;
      text-transform: uppercase;
      white-space: nowrap;
    }

    .status::before { width: 9px; height: 9px; flex-shrink: 0; border-radius: 50%; background: #77827b; content: ""; }
    .status[data-state="connected"] { border-color: var(--green); color: var(--ink); }
    .status[data-state="connected"]::before { background: var(--green); box-shadow: 0 0 0 4px rgba(185, 229, 59, .18), 0 0 8px var(--green); }
    .status[data-state="disconnected"]::before { animation: blink 1.2s ease-in-out infinite; }
    .status[data-state="error"] { border-color: var(--red); color: #ffb8ad; }
    .status[data-state="error"]::before { background: var(--red); }

    @keyframes blink { 50% { opacity: .25; } }

    /* Panel y botones */
    section {
      position: relative;
      margin-top: 18px;
      padding: 18px;
      border: 1px solid var(--line);
      border-left: 3px solid var(--accent);
      background: var(--panel);
      box-shadow: 0 14px 32px rgba(0, 0, 0, .28);
    }

    h2 { margin: 0 0 14px; color: var(--muted); font-size: 12px; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }

    button {
      min-height: 44px;
      padding: 0 16px;
      border: 1px solid var(--line);
      border-radius: 2px;
      color: var(--ink);
      background: #222722;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      transition: background-color .15s ease, border-color .15s ease;
      touch-action: manipulation;
      -webkit-user-select: none;
      user-select: none;
    }

    button:hover { border-color: var(--accent); background: #2c3329; }
    button:disabled { cursor: not-allowed; opacity: .45; }
    button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }

    .btn-accent { border-color: var(--accent); color: #14170b; background: var(--accent); text-transform: uppercase; }
    .btn-accent:hover { background: #e5f856; }

    /* Modos */
    .modes { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 9px; }

    .mode-button {
      position: relative;
      display: flex;
      min-height: 78px;
      flex-direction: column;
      align-items: flex-start;
      justify-content: center;
      gap: 4px;
      padding: 10px 12px;
      overflow: hidden;
      background: linear-gradient(145deg, #202520, #141814);
      text-align: left;
      animation: mode-enter .3s ease-out both;
    }

    .mode-button:nth-child(2) { animation-delay: .04s; }
    .mode-button:nth-child(3) { animation-delay: .08s; }
    .mode-button:nth-child(4) { animation-delay: .12s; }

    .mode-button::after {
      position: absolute;
      right: -14px;
      bottom: 8px;
      width: 40px;
      height: 3px;
      background: var(--red);
      content: "";
      transform: skewX(-38deg);
    }

    .mode-icon { width: 18px; height: 18px; margin-bottom: 2px; color: var(--accent); }
    .mode-title { font-family: var(--font-display); font-size: 16px; font-weight: 400; letter-spacing: .03em; line-height: 1.05; text-transform: uppercase; }
    .mode-description { color: var(--muted); font-size: 12px; font-weight: 400; line-height: 1.2; }

    .mode-button[aria-pressed="true"] { border-color: var(--accent); color: #14170b; background: var(--accent); box-shadow: 0 0 22px var(--accent-glow); }
    .mode-button[aria-pressed="true"] .mode-icon { color: #14170b; }
    .mode-button[aria-pressed="true"] .mode-description { color: #252a12; font-weight: 700; }
    .mode-button[aria-pressed="true"]::after { background: #14170b; }

    .instructions-bar { margin-top: 14px; padding: 9px 12px; border: 1px solid #282e28; border-left: 2px solid var(--muted); background: #101411; }
    .instructions { margin: 0; font-size: 13px; line-height: 1.4; }

    /* Vistas de control */
    .control-view { margin-top: 16px; padding-top: 16px; border-top: 1px solid #3c4435; animation: reveal .18s ease-out; transition: opacity .2s; }

    @keyframes reveal { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: translateY(0); } }
    @keyframes mode-enter { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }

    /* Sin conexión: controles y barras atenuados */
    body:has(#status[data-state="disconnected"]) .control-view:not(#cameraView),
    body:has(#status[data-state="disconnected"]) .vector-bars { opacity: .45; }

    .telemetry { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-top: 14px; }
    .reading { padding: 10px 12px; border: 1px solid var(--line); border-left: 3px solid var(--accent); background: #101411; }
    .reading-label { display: block; color: var(--muted); font-size: 11px; letter-spacing: .04em; text-transform: uppercase; }
    .reading-value { display: block; margin-top: 2px; font: 700 21px var(--font-mono); font-variant-numeric: tabular-nums; }
    #valBeta::after, #valGamma::after { content: "°"; color: var(--muted); }

    /* Giroscopio: horizonte (solo visual) */
    #enableGyroscope { margin-bottom: 4px; }
    .gyro-stage { display: grid; grid-template-columns: 160px 1fr; gap: 16px; align-items: center; margin-top: 10px; }
    .gyro-stage .telemetry { margin-top: 0; }

    .horizon-dial {
      position: relative;
      width: 156px;
      height: 156px;
      margin: 0 auto;
      overflow: hidden;
      border: 2px solid var(--line-bright);
      border-radius: 50%;
      background:
        radial-gradient(circle, rgba(215, 237, 56, .08) 0 22%, transparent 23% 54%, rgba(255, 255, 255, .03) 55% 100%),
        #101411;
    }

    .horizon-dial::before, .horizon-dial::after { position: absolute; background: #424a3b; content: ""; }
    .horizon-dial::before { top: 0; left: 50%; width: 1px; height: 100%; }
    .horizon-dial::after { top: 50%; left: 0; width: 100%; height: 1px; }

    .horizon-bubble {
      position: absolute;
      top: 50%;
      left: 50%;
      width: 24px;
      height: 24px;
      border: 2px solid #12150a;
      border-radius: 50%;
      background: var(--accent);
      box-shadow: 0 0 14px rgba(215, 237, 56, .6);
      transform: translate(-50%, -50%);
      transition: transform .06s linear;
    }

    /* Joystick */
    .joystick {
      position: relative;
      width: min(260px, 76vw, 52vh);
      aspect-ratio: 1;
      margin: 12px auto 14px;
      overflow: hidden;
      border: 2px solid var(--line-bright);
      border-radius: 50%;
      background: radial-gradient(circle, #2a3026 0 18%, #1f241e 19% 46%, #161a16 47% 74%, #101411 75% 100%);
      box-shadow: inset 0 0 28px rgba(0, 0, 0, .7), 0 8px 24px rgba(0, 0, 0, .35);
      touch-action: none;
      -webkit-user-select: none;
      user-select: none;
      cursor: grab;
    }

    .joystick:active { border-color: var(--accent); cursor: grabbing; }
    .joystick::before, .joystick::after { position: absolute; background: #4e5642; content: ""; pointer-events: none; }
    .joystick::before { top: 0; left: 50%; width: 1px; height: 100%; }
    .joystick::after { top: 50%; left: 0; width: 100%; height: 1px; }
    .joystick-ring { position: absolute; inset: 24%; border: 1px dashed rgba(215, 237, 56, .25); border-radius: 50%; pointer-events: none; }

    .joystick-stick {
      position: absolute;
      z-index: 2;
      top: 50%;
      left: 50%;
      display: grid;
      width: 74px;
      aspect-ratio: 1;
      place-items: center;
      border: 3px solid #f4f5df;
      border-radius: 50%;
      background: radial-gradient(circle at 35% 30%, #ff735c 0%, var(--red) 62%, #b5321e 100%);
      box-shadow: 0 6px 18px rgba(0, 0, 0, .65), 0 0 20px var(--red-glow);
      transform: translate(-50%, -50%);
      pointer-events: none;
    }

    .joystick-stick::after { width: 22px; height: 22px; border: 1px solid rgba(255, 255, 255, .45); border-radius: 50%; content: ""; }
    .joystick:active .joystick-stick { box-shadow: 0 6px 18px rgba(0, 0, 0, .65), 0 0 28px rgba(240, 82, 57, .55); }
    .joystick-stick.spring-back { transition: transform .16s cubic-bezier(.22, 1.25, .36, 1); }

    /* WASD (indicadores del teclado) */
    .keypad { display: grid; grid-template-columns: repeat(3, minmax(64px, 84px)); gap: 10px; justify-content: center; margin: 14px auto 8px; -webkit-user-select: none; user-select: none; }
    .keypad .empty { visibility: hidden; }

    .key {
      display: flex;
      min-height: 68px;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 8px;
      border: 1px solid var(--line-bright);
      border-bottom-width: 4px;
      border-radius: 3px;
      color: var(--accent);
      background: linear-gradient(180deg, #242a24 0%, #181c18 100%);
      box-shadow: 0 5px 0 #090b09;
      transition: transform .06s ease-out, background-color .06s ease-out, box-shadow .06s ease-out;
    }

    .key kbd { font: 700 22px var(--font-mono); line-height: 1; }
    .key span { margin-top: 4px; color: var(--muted); font-size: 10px; letter-spacing: .05em; text-transform: uppercase; }

    .key.on { transform: translateY(3px); border-color: var(--accent); border-bottom-width: 1px; color: #14170b; background: var(--accent); box-shadow: 0 2px 0 #090b09, 0 0 18px var(--accent-glow); }
    .key.on span { color: #14170b; }

    /* Cámara MJPEG */
    .camera-frame {
      position: relative;
      display: grid;
      aspect-ratio: 16 / 9;
      min-height: 240px;
      overflow: hidden;
      border: 1px solid var(--line-bright);
      background: #080a08;
      place-items: center;
      text-align: center;
    }

    .camera-feed { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; }
    .camera-hud { position: absolute; z-index: 2; inset: 10px; pointer-events: none; }
    .hud-row { display: flex; justify-content: space-between; color: var(--accent); font: 11px var(--font-mono); text-shadow: 0 1px 3px #000; }
    .hud-crosshair { position: absolute; top: 50%; left: 50%; width: 44px; height: 44px; border: 1px solid rgba(215, 237, 56, .45); border-radius: 50%; transform: translate(-50%, -50%); }

    .camera-placeholder { position: relative; z-index: 3; max-width: 380px; margin: 16px; padding: 16px; border: 1px solid var(--line); background: rgba(12, 15, 13, .92); }
    .camera-placeholder .mode-icon { width: 28px; height: 28px; }
    .camera-placeholder strong { display: block; margin: 6px 0; color: var(--accent); letter-spacing: .05em; text-transform: uppercase; }
    .camera-placeholder p { margin: 0; color: var(--muted); font-size: 13px; line-height: 1.45; }

    /* Barras de intensidad: derecha/adelante = lima, izquierda/atrás = rojo */
    .vector-bars { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-top: 14px; padding-top: 12px; border-top: 1px solid #262c26; transition: opacity .2s; }
    .bar-card { padding: 8px 10px; border: 1px solid #282e28; background: #101411; }
    .bar-head { display: flex; justify-content: space-between; margin-bottom: 6px; color: var(--muted); font-size: 11px; letter-spacing: .04em; text-transform: uppercase; }
    .bar-value { color: var(--accent); font-family: var(--font-mono); font-variant-numeric: tabular-nums; }
    .bar-card[data-sign="neg"] .bar-value { color: var(--red); }
    .meter-track { position: relative; height: 8px; overflow: hidden; background: #1d221d; }
    .meter-center { position: absolute; z-index: 2; top: 0; bottom: 0; left: 50%; width: 2px; margin-left: -1px; background: var(--ink); }
    .meter-fill { position: absolute; top: 0; bottom: 0; left: 50%; width: 0; background: var(--accent); transition: left .06s linear, width .06s linear; }
    .bar-card[data-sign="neg"] .meter-fill { background: var(--red); }
    .bar-ends { display: flex; justify-content: space-between; margin-top: 5px; color: var(--muted); font-size: 10px; letter-spacing: .04em; text-transform: uppercase; }

    /* Última instrucción enviada */
    .ws-monitor { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; margin-top: 14px; padding: 8px 12px; border: 1px solid #282e28; background: #0b0e0c; font: 11px/1.45 var(--font-mono); }
    .ws-monitor-label { color: var(--muted); white-space: nowrap; }
    .ws-payload { min-width: 0; color: var(--green); overflow-wrap: anywhere; text-align: right; }

    @media (prefers-reduced-motion: reduce) {
      *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; }
    }

    @media (max-width: 600px) {
      main { padding-top: 16px; }
      header { align-items: flex-start; flex-direction: column; }
      section { padding: 14px; }
      .modes { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .gyro-stage { grid-template-columns: 1fr; }
      .vector-bars { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <main>
    <header>
      <div>
        <p class="eyebrow"><span>Street series / unidad de control</span><span class="eyebrow-tag">ESP32-WS</span></p>
        <h1>Autito</h1>
      </div>
      <div id="status" class="status" data-state="disconnected" role="status">Desconectado</div>
    </header>

    <section aria-labelledby="modeTitle">
      <h2 id="modeTitle">Selecciona tu manejo</h2>
      <div class="modes" role="group" aria-label="Modo de control">
        <button class="mode-button" type="button" data-mode="gyroscope" aria-pressed="true">
          <svg class="mode-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><rect x="6" y="2" width="12" height="20" rx="2"/><path d="M12 18h.01M2 12h2M20 12h2"/></svg>
          <span class="mode-title">Giroscopio</span><span class="mode-description">Sensor del móvil</span>
        </button>
        <button class="mode-button" type="button" data-mode="joystick" aria-pressed="false">
          <svg class="mode-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="3" fill="currentColor"/></svg>
          <span class="mode-title">Joystick</span><span class="mode-description">Control táctil</span>
        </button>
        <button class="mode-button" type="button" data-mode="wasd" aria-pressed="false">
          <svg class="mode-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><rect x="9" y="3" width="6" height="6" rx="1"/><rect x="3" y="13" width="6" height="6" rx="1"/><rect x="9" y="13" width="6" height="6" rx="1"/><rect x="15" y="13" width="6" height="6" rx="1"/></svg>
          <span class="mode-title">WASD</span><span class="mode-description">Teclas W, A, S, D</span>
        </button>
        <button class="mode-button" type="button" data-mode="camera" aria-pressed="false">
          <svg class="mode-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M23 7l-7 5 7 5V7z"/><rect x="1" y="5" width="15" height="14" rx="2"/></svg>
          <span class="mode-title">Solo cámara</span><span class="mode-description">Sin control de marcha</span>
        </button>
      </div>

      <div class="instructions-bar"><p id="instructions" class="instructions" role="status">Conéctate y activa el sensor para comenzar.</p></div>

      <div id="gyroView" class="control-view">
        <button id="enableGyroscope" class="btn-accent" type="button">Activar giroscopio</button>
        <div class="gyro-stage">
          <div class="horizon-dial" aria-hidden="true"><div id="horizonBubble" class="horizon-bubble"></div></div>
          <div class="telemetry" aria-live="polite">
            <div class="reading"><span class="reading-label">Adelante / atrás</span><span id="valBeta" class="reading-value">0</span></div>
            <div class="reading"><span class="reading-label">Izquierda / derecha</span><span id="valGamma" class="reading-value">0</span></div>
          </div>
        </div>
      </div>

      <div id="joystickView" class="control-view hidden">
        <div id="joystick" class="joystick" aria-label="Joystick táctil">
          <div class="joystick-ring"></div>
          <div id="joystickStick" class="joystick-stick"></div>
        </div>
        <div class="telemetry" aria-live="polite">
          <div class="reading"><span class="reading-label">Adelante / atrás</span><span id="valY" class="reading-value">0.00</span></div>
          <div class="reading"><span class="reading-label">Izquierda / derecha</span><span id="valX" class="reading-value">0.00</span></div>
        </div>
      </div>

      <div id="wasdView" class="control-view hidden">
        <div class="keypad" aria-label="Teclas W A S D">
          <div class="empty"></div><div class="key" data-dir="forward"><kbd>W</kbd><span>Adelante</span></div><div class="empty"></div>
          <div class="key" data-dir="left"><kbd>A</kbd><span>Izq.</span></div>
          <div class="key" data-dir="backward"><kbd>S</kbd><span>Atrás</span></div>
          <div class="key" data-dir="right"><kbd>D</kbd><span>Der.</span></div>
        </div>
      </div>

      <div id="cameraView" class="control-view hidden">
        <div class="camera-frame">
          <img id="cameraFeed" class="camera-feed hidden" alt="Video en vivo de la cámara del autito">
          <div class="camera-hud">
            <div class="hud-row"><span>CAM-01 // FPV</span><span id="cameraHudStatus">STANDBY</span></div>
            <div class="hud-crosshair"></div>
          </div>
          <div id="cameraPlaceholder" class="camera-placeholder" role="status" aria-live="polite">
            <svg class="mode-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M23 7l-7 5 7 5V7z"/><rect x="1" y="5" width="15" height="14" rx="2"/></svg>
            <strong id="cameraState">Conectando cámara</strong>
            <p id="cameraMessage">Esperando el stream de video.</p>
          </div>
        </div>
      </div>

      <div id="bars" class="vector-bars" aria-label="Intensidad del control">
        <div id="barDrive" class="bar-card" data-sign="pos" role="meter" aria-label="Tracción" aria-valuemin="-100" aria-valuemax="100" aria-valuenow="0">
          <div class="bar-head"><span>Tracción</span><span class="bar-value">0%</span></div>
          <div class="meter-track"><div class="meter-center"></div><div class="meter-fill"></div></div>
          <div class="bar-ends"><span>Atrás</span><span>Adelante</span></div>
        </div>
        <div id="barSteer" class="bar-card" data-sign="pos" role="meter" aria-label="Dirección" aria-valuemin="-100" aria-valuemax="100" aria-valuenow="0">
          <div class="bar-head"><span>Dirección</span><span class="bar-value">0%</span></div>
          <div class="meter-track"><div class="meter-center"></div><div class="meter-fill"></div></div>
          <div class="bar-ends"><span>Izquierda</span><span>Derecha</span></div>
        </div>
      </div>

      <div class="ws-monitor" title="Última instrucción enviada al ESP32">
        <span class="ws-monitor-label">TX JSON &gt;</span>
        <code id="wsPayload" class="ws-payload">—</code>
      </div>
    </section>
  </main>

  <script>
    const statusElement = document.querySelector("#status");
    const instructions = document.querySelector("#instructions");
    const enableGyroscopeButton = document.querySelector("#enableGyroscope");
    const modeButtons = [...document.querySelectorAll("[data-mode]")];
    const cameraFeed = document.querySelector("#cameraFeed");
    const cameraPlaceholder = document.querySelector("#cameraPlaceholder");
    const cameraState = document.querySelector("#cameraState");
    const cameraMessage = document.querySelector("#cameraMessage");
    const cameraHudStatus = document.querySelector("#cameraHudStatus");
    const cameraServerUrl = `http://${window.location.hostname}:82`;
    const cameraHealthUrl = `${cameraServerUrl}/health`;
    const cameraStreamUrl = `${cameraServerUrl}/stream`;
    let cameraRetryTimer = null;
    let cameraFrameTimer = null;
    let cameraRequestController = null;
    const wsPayload = document.querySelector("#wsPayload");
    const keyState = { forward: false, backward: false, left: false, right: false };
    const keyMap = {
      wasd: { w: "forward", s: "backward", a: "left", d: "right" }
    };

    let socket = null;
    let currentMode = "gyroscope";
    let gyroscopeEnabled = false;
    let joystickActive = false;
    let lastJoystickSend = 0;
    let lastGyroscopeSend = 0;
    let reconnectTimer = null;

    function setStatus(text, state) {
      statusElement.textContent = text;
      statusElement.dataset.state = state;
    }

    /* ---- Solo visual: barras, horizonte y teclas ---- */
    const GYRO_RANGE = 45;        // grados de inclinación = 100% de la barra
    const GYRO_FORWARD_SIGN = -1; // -1: inclinar el móvil hacia adelante (beta menor) = adelante. Cambiar a 1 para invertir
    const HORIZON_RADIUS = 54;    // recorrido de la burbuja en px
    const barDrive = document.querySelector("#barDrive");
    const barSteer = document.querySelector("#barSteer");
    const horizonBubble = document.querySelector("#horizonBubble");
    const clamp = (v) => Math.max(-1, Math.min(1, Number.isFinite(v) ? v : 0));

    function setBar(bar, value) {
      const v = clamp(value);
      const pct = Math.round(v * 100);
      const fill = bar.querySelector(".meter-fill");
      fill.style.left = (v >= 0 ? 50 : 50 + v * 50) + "%";
      fill.style.width = Math.abs(v) * 50 + "%";
      bar.dataset.sign = v < 0 ? "neg" : "pos";
      bar.querySelector(".bar-value").textContent = pct + "%";
      bar.setAttribute("aria-valuenow", pct);
    }

    function updateBars(forward, side) {
      setBar(barDrive, forward);
      setBar(barSteer, side);
    }

    function showGyro(beta, gamma) {
      document.querySelector("#valBeta").textContent = Math.round(beta);
      document.querySelector("#valGamma").textContent = Math.round(gamma);
      const forward = clamp(GYRO_FORWARD_SIGN * beta / GYRO_RANGE);
      const side = clamp(gamma / GYRO_RANGE);
      horizonBubble.style.transform =
        `translate(calc(-50% + ${side * HORIZON_RADIUS}px), calc(-50% + ${-forward * HORIZON_RADIUS}px))`;
      updateBars(forward, side);
    }

    function paintKeys() {
      document.querySelectorAll("[data-dir]").forEach((k) => k.classList.toggle("on", keyState[k.dataset.dir]));
      updateBars(
        (keyState.forward ? 1 : 0) - (keyState.backward ? 1 : 0),
        (keyState.right ? 1 : 0) - (keyState.left ? 1 : 0)
      );
    }

    /* ---- Envío: el monitor muestra solo lo que realmente salió por el WebSocket ---- */
    function send(message) {
      if (!socket || socket.readyState !== WebSocket.OPEN) return false;
      socket.send(JSON.stringify(message));
      // Solo para mostrar: se redondea a 2 decimales (lo enviado no se modifica)
      wsPayload.textContent = JSON.stringify(message, (k, v) => typeof v === "number" ? Math.round(v * 100) / 100 : v);
      return true;
    }

    function sendNeutral(mode) {
      if (mode === "gyroscope") {
        send({ type: "control", data: { beta: 0, gamma: 0 } });
        showGyro(0, 0);
      } else if (mode === "joystick") {
        send({ type: "control", data: { x: 0, y: 0 } });
        updateJoystickDisplay(0, 0, true);
      } else if (mode === "wasd") {
        Object.keys(keyState).forEach((key) => { keyState[key] = false; });
        send({ type: "control", data: { ...keyState } });
        paintKeys();
      }
    }

    function scheduleCameraRetry() {
      if (cameraRetryTimer) clearTimeout(cameraRetryTimer);
      cameraRetryTimer = setTimeout(() => {
        cameraRetryTimer = null;
        startCameraStream();
      }, 3000);
    }

    async function startCameraStream() {
      if (cameraRetryTimer) clearTimeout(cameraRetryTimer);
      cameraRetryTimer = null;
      if (cameraRequestController) cameraRequestController.abort();
      const requestController = new AbortController();
      cameraRequestController = requestController;
      cameraState.textContent = "Conectando cámara";
      cameraMessage.textContent = "Comprobando Wi-Fi y sensor de cámara.";
      cameraHudStatus.textContent = "CONNECTING";
      cameraPlaceholder.classList.remove("hidden");
      cameraFeed.classList.add("hidden");

      const requestTimeout = setTimeout(() => requestController.abort(), 4000);
      try {
        const response = await fetch(`${cameraHealthUrl}?t=${Date.now()}`, {
          cache: "no-store",
          signal: requestController.signal
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const health = await response.json();
        if (currentMode !== "camera" || cameraRequestController !== requestController) return;

        if (health.camera !== "ready") {
          cameraState.textContent = "Sensor no inicializado";
          cameraMessage.textContent = "La cámara responde, pero no inicializó el OV3660. Revisa el modelo de la placa y el pinout.";
          cameraHudStatus.textContent = "CAM ERROR";
          scheduleCameraRetry();
          return;
        }

        cameraState.textContent = "Cámara conectada";
        cameraMessage.textContent = "Esperando el primer fotograma.";
        cameraHudStatus.textContent = "CAMERA READY";
        cameraFeed.src = `${cameraStreamUrl}?t=${Date.now()}`;
        if (cameraFrameTimer) clearTimeout(cameraFrameTimer);
        cameraFrameTimer = setTimeout(() => {
          if (currentMode !== "camera") return;
          if (cameraFeed.naturalWidth > 0) {
            markCameraLive();
            return;
          }
          cameraFeed.removeAttribute("src");
          cameraState.textContent = "No llegan imágenes";
          cameraMessage.textContent = "La cámara está en la red, pero no entrega fotogramas. Revisa el sensor y su flex.";
          cameraHudStatus.textContent = "NO VIDEO";
          scheduleCameraRetry();
        }, 8000);
      } catch {
        if (currentMode !== "camera" || cameraRequestController !== requestController) return;
        cameraState.textContent = "Sin respuesta de cámara";
        cameraMessage.textContent = "No responde el servidor de cámara. Comprueba la inicialización del sensor y el monitor serie.";
        cameraHudStatus.textContent = "OFFLINE";
        scheduleCameraRetry();
      } finally {
        clearTimeout(requestTimeout);
        if (cameraRequestController === requestController) cameraRequestController = null;
      }
    }

    function stopCameraStream() {
      if (cameraRetryTimer) clearTimeout(cameraRetryTimer);
      if (cameraFrameTimer) clearTimeout(cameraFrameTimer);
      if (cameraRequestController) cameraRequestController.abort();
      cameraRetryTimer = null;
      cameraFrameTimer = null;
      cameraRequestController = null;
      cameraFeed.removeAttribute("src");
      cameraFeed.classList.add("hidden");
      cameraPlaceholder.classList.remove("hidden");
      cameraState.textContent = "Cámara desconectada";
      cameraMessage.textContent = "Selecciona Solo cámara para iniciar el video.";
      cameraHudStatus.textContent = "STANDBY";
    }

    function markCameraLive() {
      if (cameraFrameTimer) clearTimeout(cameraFrameTimer);
      cameraFrameTimer = null;
      cameraPlaceholder.classList.add("hidden");
      cameraFeed.classList.remove("hidden");
      cameraHudStatus.textContent = "LIVE";
    }

    cameraFeed.addEventListener("load", () => {
      if (currentMode !== "camera") return;
      markCameraLive();
    });
    cameraFeed.addEventListener("error", () => {
      if (currentMode !== "camera") return;
      if (cameraFrameTimer) clearTimeout(cameraFrameTimer);
      cameraFrameTimer = null;
      cameraFeed.removeAttribute("src");
      cameraPlaceholder.classList.remove("hidden");
      cameraFeed.classList.add("hidden");
      cameraState.textContent = "Se perdió el stream";
      cameraMessage.textContent = "Se perdió la conexión de video; se volverá a comprobar la cámara.";
      cameraHudStatus.textContent = "RETRYING";
      scheduleCameraRetry();
    });

    function setMode(mode, stopPrevious = true) {
      const previousMode = currentMode;
      if (stopPrevious) sendNeutral(previousMode);
      currentMode = mode;
      if (previousMode === "camera" && mode !== "camera") stopCameraStream();
      modeButtons.forEach((button) => {
        button.setAttribute("aria-pressed", String(button.dataset.mode === mode));
      });

      document.querySelector("#gyroView").classList.toggle("hidden", mode !== "gyroscope");
      document.querySelector("#joystickView").classList.toggle("hidden", mode !== "joystick");
      document.querySelector("#wasdView").classList.toggle("hidden", mode !== "wasd");
      document.querySelector("#cameraView").classList.toggle("hidden", mode !== "camera");
      document.querySelector("#bars").classList.toggle("hidden", mode === "camera");
      enableGyroscopeButton.hidden = mode !== "gyroscope" || gyroscopeEnabled;

      if (mode === "camera") {
        instructions.textContent = "Vista de cámara en vivo.";
        if (!cameraFeed.hasAttribute("src")) startCameraStream();
        return;
      }

      send({ type: "select", driver: mode });
      if (mode === "joystick") instructions.textContent = "Arrastra el control. Al soltarlo, el auto vuelve a neutro.";
      else if (mode === "wasd") instructions.textContent = "Usa W, A, S y D para dirigir.";
      else instructions.textContent = gyroscopeEnabled ? "Giroscopio activo." : "Activa el sensor para comenzar.";
    }

    function connect() {
      if (reconnectTimer) clearTimeout(reconnectTimer);
      if (socket) socket.close();
      setStatus("Conectando", "disconnected");

      const connection = new WebSocket(`ws://${window.location.hostname}:81`);
      socket = connection;
      connection.addEventListener("open", () => {
        if (socket !== connection) return;
        setStatus("Conectado", "connected");
        setMode(currentMode, false);
        if (currentMode !== "camera") sendNeutral(currentMode);
      });
      connection.addEventListener("close", () => {
        if (socket !== connection) return;
        setStatus("Desconectado", "disconnected");
        reconnectTimer = setTimeout(connect, 2000);
      });
      connection.addEventListener("error", () => {
        if (socket === connection) setStatus("Error de conexión", "error");
      });
    }

    /* ---- Joystick ---- */
    const joystick = document.querySelector("#joystick");
    const joystickStick = document.querySelector("#joystickStick");

    function updateJoystickDisplay(x, y, springBack = false) {
      joystickStick.classList.toggle("spring-back", springBack);
      const rect = joystick.getBoundingClientRect();
      const radius = Math.max(1, rect.width / 2 - 40);
      joystickStick.style.transform =
        `translate(calc(-50% + ${x * radius}px), calc(-50% + ${y * radius}px))`;
      document.querySelector("#valX").textContent = x.toFixed(2);
      document.querySelector("#valY").textContent = y.toFixed(2);
      updateBars(-y, x);
    }

    function moveJoystick(event, force = false) {
      if (!joystickActive || currentMode !== "joystick") return;
      const rect = event.currentTarget.getBoundingClientRect();
      const radius = Math.max(1, rect.width / 2 - 40);
      let x = (event.clientX - (rect.left + rect.width / 2)) / radius;
      let y = (event.clientY - (rect.top + rect.height / 2)) / radius;
      const magnitude = Math.hypot(x, y);
      if (magnitude > 1) {
        x /= magnitude;
        y /= magnitude;
      }
      updateJoystickDisplay(x, y);

      const now = Date.now();
      if (force || now - lastJoystickSend >= 80) {
        send({ type: "control", data: { x, y } });
        lastJoystickSend = now;
      }
    }

    joystick.addEventListener("pointerdown", (event) => {
      if (currentMode !== "joystick") return;
      joystickActive = true;
      joystick.setPointerCapture(event.pointerId);
      moveJoystick(event, true);
    });
    joystick.addEventListener("pointermove", (event) => moveJoystick(event));
    function releaseJoystick() {
      if (!joystickActive) return;
      joystickActive = false;
      updateJoystickDisplay(0, 0, true);
      send({ type: "control", data: { x: 0, y: 0 } });
    }
    joystick.addEventListener("pointerup", releaseJoystick);
    joystick.addEventListener("pointercancel", releaseJoystick);

    /* ---- WASD ---- */
    function getKeyDirection(event) {
      const mapping = keyMap[currentMode];
      return mapping ? (mapping[event.key] || mapping[event.key.toLowerCase()]) : null;
    }

    document.addEventListener("keydown", (event) => {
      if (currentMode !== "wasd") return;
      const direction = getKeyDirection(event);
      if (!direction) return;
      event.preventDefault();
      if (keyState[direction]) return;
      keyState[direction] = true;
      send({ type: "control", data: { ...keyState } });
      paintKeys();
    });

    document.addEventListener("keyup", (event) => {
      if (currentMode !== "wasd") return;
      const direction = getKeyDirection(event);
      if (!direction) return;
      event.preventDefault();
      keyState[direction] = false;
      send({ type: "control", data: { ...keyState } });
      paintKeys();
    });

    window.addEventListener("blur", () => {
      if (currentMode === "wasd") sendNeutral(currentMode);
      if (currentMode === "joystick") releaseJoystick();
    });

    /* ---- Giroscopio y arranque ---- */
    modeButtons.forEach((button) => {
      button.addEventListener("click", () => setMode(button.dataset.mode));
    });
    enableGyroscopeButton.addEventListener("click", async () => {
      if (!("DeviceOrientationEvent" in window)) {
        instructions.textContent = "Este navegador no ofrece orientación del dispositivo.";
        return;
      }

      if (typeof DeviceOrientationEvent.requestPermission === "function") {
        try {
          const permission = await DeviceOrientationEvent.requestPermission();
          if (permission !== "granted") throw new Error("Permiso denegado");
        } catch {
          instructions.textContent = "No se concedió permiso para usar el giroscopio.";
          return;
        }
      }

      gyroscopeEnabled = true;
      enableGyroscopeButton.hidden = true;
      instructions.textContent = "Giroscopio activo. Mueve el dispositivo para enviar datos.";
    });

    connect();

    window.addEventListener("deviceorientation", (event) => {
      if (!gyroscopeEnabled || currentMode !== "gyroscope" || event.beta === null || event.gamma === null) return;
      const now = Date.now();
      if (now - lastGyroscopeSend < 80) return;
      showGyro(event.beta, event.gamma);
      send({ type: "control", data: { beta: event.beta, gamma: event.gamma } });
      lastGyroscopeSend = now;
    });
  </script>
</body>
</html>
)rawliteral";