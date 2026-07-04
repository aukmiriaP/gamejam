# CIGA2026 GameJam 项目交接说明

更新时间：2026-07-04

## 项目定位

本项目是以 **Anchor / 锚** 为主题的 2D Unity 原型。当前已完成一个合并后的教学关卡，用来让玩家熟悉基础移动、锚定摆荡、存档点、氧气、碎石流、氧气补给和终点提示等核心机制。

当前主场景：

```text
Assets/Scenes/TutorialLevel.unity
```

`Assets/Scenes/SampleScene.unity` 仍保留为原始教学关副本来源，后续建议优先在 `TutorialLevel` 上继续整合。

当前目标不是完整四关内容，而是一个可玩的教学关模板。后续四关可以在这个基础上重组机制、替换美术和音频、扩展关卡结构。

## 已实现功能

### 玩家锚定移动

核心脚本：

```text
Assets/Scripts/Player/PlayerShip.cs
Assets/Scripts/Anchor/AnchorProjectile.cs
Assets/Scripts/World/CelestialBody.cs
```

已实现：

- 玩家启动后具备向右的基础运动能力。
- 鼠标左键发射锚。
- 锚命中带 `CelestialBody` 的天体后，玩家进入绕该天体的锚定坠落运动。
- 锚定后锚链半径会自动缩短，飞船被持续拉向天体。
- 越靠近天体，角速度越快，释放速度也会根据本次锚定的靠近进度提升。
- 每次锚定的释放速度独立映射在 `moveSpeed` 到 `maxOrbitReleaseSpeed` 之间，避免连续锚定多个行星后速度复利叠加失控。
- 玩家需要在撞上天体前再次点击左键释放锚。
- 释放时会沿当前切线方向飞出，释放时机决定飞出方向和速度。
- 撞上天体后会从当前存档点重生。
- 锚命中 `Checkpoint` 后，玩家进入直线钩锁牵引状态，不绕行、不受行星引力，沿钩锁方向靠近命中点。
- 抵达 Checkpoint 钩锁命中点后，会走正常存档点激活流程。
- 锚超出射程、命中障碍或释放后会销毁。
- 锚可以命中氧气补给道具，命中后直接收集。

当前 `PlayerShip` 可调参数：

```text
anchorChainShortenSpeed
orbitSpeedGainExponent
maxOrbitReleaseSpeed
planetImpactPadding
fallbackShipCollisionRadius
```

### 轨迹预测

核心脚本：

```text
Assets/Scripts/Player/TrajectoryPredictor.cs
```

已实现：

- 玩家处于绕行状态时显示切线方向预测线。
- 用虚线材质显示释放后的飞行方向。

### 摄像机跟随与边界限制

核心脚本：

```text
Assets/Scripts/Camera/SimpleFollowCamera.cs
```

已实现：

- 摄像机跟随玩家。
- 支持绑定上下两个危险区 Collider，限制相机中心在安全区域内。
- 注意：2D 正交相机的视野范围由 `Camera > Orthographic Size` 控制，不由 Transform Scale 控制。

### 存档点系统

核心脚本：

```text
Assets/Scripts/World/Checkpoint.cs
```

已实现：

- Checkpoint 支持圆形或矩形触发区：
  - `colliderShape = Circle` 使用 `triggerRadius`。
  - `colliderShape = Box` 使用 `triggerSize`，适合空间站这类长方形贴图。
- 玩家碰到普通存档点后：
  - 存档点点亮。
  - 玩家位置重置到存档点中心。
  - 玩家速度清零。
  - 玩家进入等待状态。
  - 只有再次点击鼠标左键后，才会按初始速度重新出发。
- 玩家可以跳过中间存档点，直接抵达终点。
- 终点存档点支持 `finalCheckpoint` 开关。
- 玩家抵达终点后：
  - 停在终点中心。
  - 弹出完成窗口。
  - 显示存档点点亮情况，例如 `2/3`。
  - 点亮的 checkpoint 图标显示亮色，未点亮显示黑色。
- 终点存档点支持引导波纹：
  - `emitGuidanceRipples`
  - `rippleInterval`
  - `rippleSpeed`
  - `rippleMaxRadius`
  - `rippleWidth`
  - `rippleSegments`
  - `rippleColor`

当前 `Checkpoint_3` 已设置为终点并开启引导波纹。

### 关卡切换

核心脚本：

```text
Assets/Scripts/World/LevelManager.cs
```

已实现：

- 终点窗口的“进入下一关”按钮会调用 `LevelManager.LoadNextLevel()`。
- 如果场景里有 `LevelManager`，优先加载其 `nextSceneName`。
- 如果没有显式配置，则按 Build Settings 的下一场景加载。
- 如果当前场景没有 build index，则保底尝试加载 `Level_01`。
- 切关前会恢复 `Time.timeScale = 1f`，避免终点暂停状态带入下一场景。

### 死亡区

核心脚本：

```text
Assets/Scripts/World/DeathZone.cs
```

已实现：

- 玩家进入死亡区后重生。
- 重生后停在当前存档点中心，速度为 0。
- 玩家需要再次点击左键重新出发。
- 如果还没有任何存档点，则回到初始出生点。

当前场景中有上下边缘危险区，用于限制玩家飞出教学区域。

### 氧气机制

核心脚本：

```text
Assets/Scripts/Player/PlayerOxygen.cs
Assets/Scripts/World/OxygenRegion.cs
```

已实现：

- 玩家进入氧气挑战区域后开始消耗氧气。
- 左上角显示 O2 条。
- 氧气随时间持续减少。
- 氧气耗尽后，玩家从当前存档点重生。
- 当前设计中，进入氧气区域会把当前复活点设为 `Checkpoint_2`，保证区域 3 失败后从第二个存档点重新开始。
- 抵达 `Checkpoint_3` 后氧气挑战结束并重置氧气。

### 碎石流

核心脚本：

```text
Assets/Scripts/World/AsteroidStreamSpawner.cs
Assets/Scripts/World/AsteroidShard.cs
```

资源：

```text
Assets/Prefabs/AsteroidShard.prefab
Assets/Art/AsteroidShard.png
```

已实现：

- 上下危险区各有碎石流发射器。
- 发射器会随机生成碎石。
- 碎石向对侧移动。
- 玩家碰到碎石后：
  - 扣除氧气。
  - 受到击退。
  - 如果正在锚定或绕行，会脱离当前锚状态。

### 氧气补给

核心脚本：

```text
Assets/Scripts/World/OxygenPickup.cs
Assets/Scripts/World/OxygenPickupSpawner.cs
```

资源：

```text
Assets/Prefabs/OxygenPickup.prefab
Assets/Art/OxygenPickup.png
```

已实现：

- 玩家碰到氧气补给后恢复氧气。
- 锚命中氧气补给后也会收集。
- 区域内有随机氧气补给刷新器。
- 同时存在数量可由 `maxAlive` 控制。

## 当前场景结构

当前教学关已复制到 `TutorialLevel` 中，大致分为：

```text
区域 1：基础发锚 / 钩住天体 / 到达 Checkpoint_1
区域 2：连续锚点节奏 / 到达 Checkpoint_2
区域 3：氧气 + 碎石流 + 氧气补给 / 到达 Checkpoint_3
```

重要对象命名：

```text
Player
Main Camera
Checkpoint_1
Checkpoint_2
Checkpoint_3
Region1_Anchor_1
Anchor_1 ... Anchor_8
Region3_Anchor_1 ... Region3_Anchor_6
Region1_DeathZone
Region1_DeathZone (1)
Region3_BottomDanger
Region3_TopDanger
Region3_OxygenStart
Region3_TopAsteroidStream
Region3_BottomAsteroidStream
Region3_OxygenPickupSpawner
```

## 当前资源

临时灰盒 / 程序生成资源：

```text
Assets/Art/CheckpointRing.png
Assets/Art/FixedAnchorDisc.png
Assets/Art/DeathZoneFill.png
Assets/Art/AsteroidShard.png
Assets/Art/OxygenPickup.png
```

已有 Prefab：

```text
Assets/Prefabs/AnchorProjectile.prefab
Assets/Prefabs/AsteroidShard.prefab
Assets/Prefabs/OxygenPickup.prefab
```

建议后续在正式美术导入后再整理更多 Gameplay Prefab，例如：

```text
Assets/Prefabs/Gameplay/AnchorPoint.prefab
Assets/Prefabs/Gameplay/Checkpoint.prefab
Assets/Prefabs/Gameplay/DeathZone.prefab
Assets/Prefabs/Gameplay/AsteroidStreamSpawner.prefab
Assets/Prefabs/Gameplay/OxygenPickupSpawner.prefab
Assets/Prefabs/Gameplay/OxygenStartTrigger.prefab
```

## 待开发内容

### 关卡结构

已完成：

```text
Assets/Scenes/TutorialLevel.unity
Assets/Scenes/Level_01.unity
Assets/Scenes/Level_02.unity
Assets/Scenes/Level_03.unity
Assets/Scenes/Level_04.unity
```

- `TutorialLevel` 是从 `SampleScene` 复制出的教学关。
- `Level_01` 到 `Level_04` 目前是空白占位场景，已加入 Build Settings。

待开发：

- 把当前教学关机制 prefab 化，后续关卡用 prefab 组合。
- 避免多人同时直接改同一个 `.unity` 场景文件，Unity 场景冲突比较难合。

### 终点窗口与切关

待开发：

- 为每个正式关卡配置自己的下一关目标。
- 保存 checkpoint 点亮统计 / 通关评价。

### 美术资源

待导入：

- 玩家飞船 sprite。
- 不同类型锚点 sprite。
- 存档点 / 终点视觉。
- 碎石流 sprite / VFX。
- 氧气道具 sprite。
- 背景、边界、UI。

建议目录：

```text
Assets/Art/Sprites/Player/
Assets/Art/Sprites/Anchors/
Assets/Art/Sprites/Hazards/
Assets/Art/Sprites/Pickups/
Assets/Art/Sprites/UI/
Assets/Art/VFX/
Assets/Art/Materials/
```

导入 PNG 后注意：

- `Texture Type = Sprite (2D and UI)`
- 统一 `Pixels Per Unit`
- 提交 `.png` 和对应 `.png.meta`

### 音频

待导入：

- 发射锚音效。
- 命中锚点音效。
- 释放锚音效。
- 存档点激活音效。
- 终点完成音效。
- 氧气补给音效。
- 碎石命中音效。
- 教学关 BGM。

建议目录：

```text
Assets/Audio/BGM/
Assets/Audio/SFX/
```

待开发：

- `AudioManager`。
- 各机制触发音效。
- 音量设置。

### UI

当前 UI 使用 `OnGUI` 临时实现：

- 氧气条。
- 终点完成窗口。
- checkpoint 点亮图标。

后续建议改为正式 Unity UI：

- Canvas。
- OxygenBar。
- CompletionPanel。
- CheckpointProgressIcons。
- NextLevelButton。

### Git / 协作

当前 `.gitignore` 已添加，用于排除 Unity 生成目录：

```text
Library/
Temp/
Logs/
UserSettings/
Build/
Builds/
```

Unity 项目必须提交：

```text
Assets/
Packages/
ProjectSettings/
.gitignore
```

尤其注意：`Assets` 里的 `.meta` 文件必须提交。

推荐协作方式：

- `main`：稳定版本。
- 每个成员开自己的分支。
- 完成一个功能后通过 Pull Request 合并到 `main`。
- 场景文件尽量由一个人集中整合，其他人优先改 prefab、脚本、素材。

### MCP / AI Game Developer 注意事项

当前项目仍包含 MCP 相关依赖：

```text
Packages/manifest.json
  com.ivanmurzak.unity.mcp

Packages/packages-lock.json
ProjectSettings/PackageManagerSettings.asset
Assets/Plugins/NuGet/
```

如果同学没有配置 MCP server/token，Unity Editor Console 可能会出现 MCP 授权或连接失败日志。这通常不影响游戏运行，但会干扰协作成员。

建议二选一：

1. 如果团队成员都不用 MCP：从项目依赖中移除 MCP，把它当作个人本地工具。
2. 如果团队成员都用 MCP：每个人都需要配置自己的 MCP server/token。

## 已知风险 / 注意点

- 当前很多视觉资源还是临时生成图，不是最终美术。
- 当前完成窗口和氧气条使用 `OnGUI`，适合原型，不适合最终 UI。
- 场景中对象较多，后续最好 prefab 化和分组整理。
- `Checkpoint` 的完成窗口使用 `Time.timeScale = 0` 暂停游戏，后续接入正式 UI 时要注意恢复时间。
- 碎石流频率、扣氧量、氧气消耗速度都需要 Play Mode 手感调参。
- 如果多人改同一个 `SampleScene.unity`，很容易产生 Git 冲突。

## 推荐下一步

1. 决定是否从仓库移除 MCP 依赖，避免同学报错。
2. 打开 `TutorialLevel` 继续整理教学关，避免继续直接改 `SampleScene`。
3. 导入正式美术资源。
4. 制作 Gameplay Prefab。
5. 把临时 `OnGUI` UI 改成 Canvas UI。
6. 接入音频。
7. 设计后续四关，并基于 prefab 组合关卡。
