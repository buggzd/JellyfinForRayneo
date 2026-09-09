import { t } from '../../SharedUI/i18n.mjs'
export type TutorialCommand = 'up' | 'down' | 'left' | 'right' | 'enter' | 'back'
export type TutorialOutcome = 'completed' | 'skipped'

export const tutorialStorageKey = 'lucent.remote-tutorial.v1'

export const tutorialLessons: readonly {
  command: TutorialCommand
  gesture: string
  title: string
  description: string
  hint: string
  success: string
  from: number
  to: number
}[] = [
  { command: 'right', gesture: 'swipe-right', get title() { return t("向右，迈出第一步") }, get description() { return t("在手机触控板上向右滑动，\n把光点移到发亮的目标。") }, get hint() { return t("拇指贴住屏幕，向右滑一小段后抬起。") }, get success() { return t("很好！光点跟着你向右移动了。") }, from: 4, to: 5 },
  { command: 'down', gesture: 'swipe-down', get title() { return t("向下，探索下一行") }, get description() { return t("在触控板上向下滑动，\n让光点落到下一行。") }, get hint() { return t("从上向下滑动，眼镜里的焦点也会向下。") }, get success() { return t("接住了！上下滑动可以切换内容行。") }, from: 5, to: 8 },
  { command: 'left', gesture: 'swipe-left', get title() { return t("向左，轻松回身") }, get description() { return t("现在向左滑动，\n去往左边的发光目标。") }, get hint() { return t("不必找准手机上的位置，在触控板任意处左滑。") }, get success() { return t("就是这样，左右切换已经很顺手了。") }, from: 8, to: 7 },
  { command: 'up', gesture: 'swipe-up', get title() { return t("向上，回到中心") }, get description() { return t("最后向上滑动，\n回到中心，解锁一张练习卡片。") }, get hint() { return t("向上滑动，回到上一行。") }, get success() { return t("四个方向，全部掌握。卡片已解锁！") }, from: 7, to: 4 },
  { command: 'enter', gesture: 'single-tap', get title() { return t("单击，打开它") }, get description() { return t("发亮的边框代表当前选中。\n轻点一次触控板，打开这张卡片。") }, get hint() { return t("只需轻点一次，抬起拇指，等待卡片打开。") }, get success() { return t("打开了！单击会确认当前选中的内容。") }, from: 4, to: 4 },
  { command: 'back', gesture: 'double-tap', get title() { return t("双击，退回一步") }, get description() { return t("想回到刚才的画面？\n在触控板同一位置快速轻点两次。") }, get hint() { return t("哒、哒，两次轻点要连贯，中间不用停顿。") }, get success() { return t("已返回。你已经掌握基础遥控了！") }, from: 4, to: 4 },
]

export const tutorialInputLabels: Record<TutorialCommand, string> = {
  get up() { return t("向上滑动") }, get down() { return t("向下滑动") }, get left() { return t("向左滑动") }, get right() { return t("向右滑动") }, get enter() { return t("单击") }, get back() { return t("双击") },
}

export type TutorialState = {
  phase: 'welcome' | 'practice' | 'complete'
  step: number
  feedback: 'idle' | 'retry' | 'success'
  lastInput: TutorialCommand | null
  exitOpen: boolean
  choice: 0 | 1
  outcome: TutorialOutcome | null
}

export type TutorialAction =
  | { type: 'command'; command: TutorialCommand }
  | { type: 'advance' }
  | { type: 'exit' }
  | { type: 'choose'; choice: 0 | 1 }
  | { type: 'select'; choice: 0 | 1 }

export function initialTutorialState(): TutorialState {
  return { phase: 'welcome', step: 0, feedback: 'idle', lastInput: null, exitOpen: false, choice: 0, outcome: null }
}

function selectChoice(state: TutorialState, choice: 0 | 1): TutorialState {
  if (state.exitOpen) return choice === 0
    ? { ...state, exitOpen: false, choice: 0 }
    : { ...state, outcome: 'skipped' }
  if (state.phase === 'welcome') return choice === 0
    ? { ...initialTutorialState(), phase: 'practice' }
    : { ...state, outcome: 'skipped' }
  if (state.phase === 'complete') return choice === 0
    ? { ...state, outcome: 'completed' }
    : { ...initialTutorialState(), phase: 'practice' }
  return state
}

export function tutorialReducer(state: TutorialState, action: TutorialAction): TutorialState {
  if (state.outcome) return state
  if (action.type === 'advance') {
    if (state.feedback !== 'success' || state.exitOpen) return state
    return state.step === tutorialLessons.length - 1
      ? { ...state, phase: 'complete', feedback: 'idle', choice: 0 }
      : { ...state, step: state.step + 1, feedback: 'idle', lastInput: null }
  }
  if (action.type === 'exit') {
    return state.phase === 'practice'
      ? { ...state, exitOpen: true, choice: 0 }
      : { ...state, outcome: state.phase === 'complete' ? 'completed' : 'skipped' }
  }
  if (action.type === 'select') return selectChoice(state, action.choice)
  if (action.type === 'choose') return { ...state, choice: action.choice }
  const { command } = action
  if (state.exitOpen || state.phase !== 'practice') {
    if (command === 'enter') return selectChoice(state, state.choice)
    if (command === 'back') return state.exitOpen
      ? { ...state, exitOpen: false, choice: 0 }
      : { ...state, outcome: state.phase === 'complete' ? 'completed' : 'skipped' }
    return { ...state, choice: command === 'left' || command === 'up' ? 0 : 1 }
  }
  // Swipes/taps arriving during the success beat cannot solve the next lesson.
  if (state.feedback === 'success') return state
  if (command === tutorialLessons[state.step].command) {
    return { ...state, feedback: 'success', lastInput: command }
  }
  if (command === 'back') return { ...state, exitOpen: true, choice: 0 }
  return { ...state, feedback: 'retry', lastInput: command }
}

export function tutorialCommandForKey(key: string): TutorialCommand | undefined {
  const commands: Record<string, TutorialCommand | undefined> = {
    arrowup: 'up', w: 'up', arrowdown: 'down', s: 'down',
    arrowleft: 'left', a: 'left', arrowright: 'right', d: 'right',
    enter: 'enter', ' ': 'enter', escape: 'back', backspace: 'back',
  }
  return commands[key.toLowerCase()]
}

export function hasSeenRemoteTutorial() {
  try {
    const outcome = window.localStorage.getItem(tutorialStorageKey)
    return outcome === 'completed' || outcome === 'skipped'
  } catch {
    return false
  }
}

export function rememberRemoteTutorial(outcome: TutorialOutcome) {
  try {
    window.localStorage.setItem(tutorialStorageKey, outcome)
  } catch {
    // Browsing and the tutorial remain usable when WebView storage is unavailable.
  }
}
