# AutoBonusRunner 使用说明

## 版本

- 公开版本：`1.0.0`
- 内部版本：`V1.21`
- 配置版本：`44`

AutoBonusRunner 会在进入支持的 Idle Slayer Bonus Stage 后自动检测地图，并控制跳跃、攀墙、奖励领取和可用的地面疾风冲刺。游戏在后台运行时仍可继续操作。

## 安装

1. 使用 Idle Slayer Mod Manager 导入 `AutoBonusRunner.zip`，或把 `AutoBonusRunner.dll` 放入 ModLoader 的 Mods 目录。
2. 确保已经安装本 Mod 依赖的 `IdleSlayerMods.Common`。
3. 关闭其他会同时控制跳跃输入的 Mod，例如 AutoJumpMod，避免多个 Mod 同时按下或释放跳跃。
4. 启动游戏后，在 MelonLoader 日志中确认出现：

   `Plugin AutoBonusRunner v1.0.0 (internal V1.21) loaded`

## 基本操作

- 自动控制默认开启，进入支持的 Bonus Stage 后自动开始。
- 按 `U` 可以关闭或重新开启自动控制，屏幕会显示状态提示。
- 关闭自动控制后可以手动操作；检测和调试记录仍会继续工作。
- 到达奖励物品后会自动执行小跳、射箭，并在能力可用时使用地面疾风冲刺。

## 配置

配置文件位于：

`IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner.cfg`

### Mode

- `Auto`（默认）：灵魂冲刺关卡使用游戏原始灵魂要求；普通关卡只要求 1 个灵魂，以便快速完成。
- `Manual`：所有关卡都使用游戏原始灵魂要求，完整游玩并收集灵魂。
- `Skip`：所有关卡只要求 1 个灵魂。

### 其他选项

- `Enabled On Startup = true`：启动游戏时开启自动控制。
- `Toggle Key = U`：切换自动控制的按键。
- `Auto Retry Enabled = false`：出现游戏原生的一次性继续机会时，`true` 自动选择继续；`false` 自动选择放弃。不会制造无限重试。
- `Skip Start Slider = true`：奖励关开始滑块出现约一秒后自动确认。
- `Debug Mode = false`：默认只写必要日志；遇到问题时可暂时设为 `true` 获取完整诊断。
- `Configuration Version`：内部迁移字段，请勿手动修改。

## 日志

独立日志目录：

`IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner\Logs`

报告问题时，请提供发生问题后生成的最新完整日志，并说明：

- Bonus Stage 1、2 或 3
- 普通模式或灵魂冲刺
- 发生问题的小关
- 是否有手动操作

## 说明

- 本 Mod 以最终通关和自动恢复为优先目标，不保证每次都无死亡。
- 地图、速度、灵魂冲刺和墙面接触会改变实际轨迹；Mod 会使用实时速度与物理反馈重新规划。
- 如果自动控制没有反应，请先确认内部版本、`Enabled On Startup`、`Mode` 和 `U` 键状态，再检查是否有其他跳跃 Mod 同时启用。

