# StardewAI 当前工作

[文档总目录](DOCUMENTATION_INDEX_CN.md) · [编辑与交接规范](DOCUMENTATION_MAINTENANCE_CN.md)

## 2026-11-14 R2 春季第9天补水 Product 消费者缺口

- `bb24e045` 的真实112已越过旧账本登记错误，同一进程连续推进到春季第9天：本调用20条verified Product队列，原owner71已接受，最后持久日界owner68为7→8。第9天水量只够4株时原编译器先编译并接受4株，剩2株转去补水点；iteration71的补水接近结果在新消费者绑定处被拒，owner72 pending。loop退出2／监督器退出1，四归属进程已关闭。原失败保留，完整收获／销售周期未通过。
- 直接根因：原 `CropTeacherWateringMaintenance.RefillApproach` 仍固定Runtime executor，实际已固定的transition为Product；原 `CropTeacherRefillApproachNativeTrace` 默认也用Runtime-only `ReadQueue`。两文件最近提交 `09aa1acf`；这说明尚缺明确的Product消费接线，不是原编译器／移动执行器没有能力。**不得为此把旧默认读取器改成普遍接受Product**，或只改版本字符串而漏下层原请求／回执证明。
- **后续唯一实现块：** 复用既有 `ReadSelectedNativeActionQueue`，新增显式native refill trace／preparation入口；完整匹配声明executor与真实receipt，保留原queue／source／run／actor／帧／原生请求nonce／目标及资源模式。原Runtime入口默认不变，owner只对其已固定Product记录显式选择新消费者；不新增执行器／ledger、不给补水接近授作物产量信用。补真实Product协议正例、旧默认拒绝Product及错声明／错nonce／错来源反例，再用未改的112留存原件只读验完整scope，不补写native／owner接受。
- **持久性限制必须保留。** 用原 `NativeSaveBoundaryVerifier.Capture` 核实物理原档4文件／7486991bytes，SHA `b4db592b14b539b3c86875a50d7a89bfa9b645ad17d344a234d3b4e647ca8395`，与原native0067的春9保存完全一致；后4株浇水及接近水点是关闭进程后已丢失的未保存尾部。现有owner没有挥发尾部回滚协议，且readback只接受原已接受过夜／读回头；不得改写原accepted链、伪造新加载一致或直接再发已claim72。修复后从**新的隔离真实开局**重跑原完整13日／84计划，不缩小分母、不拿S部分轨迹拼成新全周期；原S所有保存、失败、native和ledger留存。若发现既有明确的合法恢复能力，先验证其原合同，不能默认存在。
- `bb24e045` CI43554993177的Backend3042/3043唯一失败是旧源码守卫要求Main直接调用旧capture名称。只更新守卫验证Main→新owner分流→原非托管capture完整委托，原断言意义保留，相关 **20/20（290ms）**；运行代码未为CI改动，原失败不回标成功。112只读performance53.85更新／秒、137次full平均1450.404ms／最大4119.631ms，仍有明显采集停顿。L1冻结、#550／#569、Teacher总验收／formal=false、local-data不提交不变。

## 2026-11-14 R2 托管日界不再进入旧账本登记

- 修正版109实际越过106断点：同S新加载后，原owner接受回农场、六株第2日浇水、回家及再次原生睡眠保存，最新第41项真实日界1→2／六株／native save均true；原存档已春季第3天。本调用报告在末次sleep后旧分支异常前记录3条verified队列，不把6个浇水primitive混为6个队列。四归属进程均退出，原失败不回改。
- 断点为新Main接续仍无条件启动旧 `CropTeacherDayBoundaryStart`，托管过夜已由原owner接受，随后却要求旧transition ledger。只在新增loop薄接线中按owner选择：托管路径不再开第二份旧日界观察／收获登记，非托管继续原capture与原save要求；不修改旧日界验证器、owner／原生睡眠或保存结果。
- 组合61项首次60通过／1旧Overnight用例在 `FileStrategyCommitmentRepository.File.Move` 出现AccessDenied；没有修改该共享存储或跳过断言。原二进制不改源码／不重建聚焦重放，失败旧用例＋新日界三项 **4/4**。保留首次61项失败，不标为61/61；本次新托管分流与非托管严格保存检查均过。之前54/54和59d16079仍独立保留，最新CI另核。
- **唯一接续：** 从同一已保存春季第3天／原owner41，当前新进程读回后继续原13日84动作计划到收获、销售、终端保存内容读回；不重发已接受第二夜、不建新owner／ledger／计划、不缩为前三天成功。完整周期／Teacher总验收／formal仍false，local-data不提交。原API块已退出撤权／授权空闲；技能已修正普通FileNotFound可用已授权窄rg纠正，不当作权限或provider故障停止。

## 2026-11-14 R2 原 owner 完成凭据接回原规划器

- `06eb63e0` 已推送，CI43551647606成功。隐藏原S／新PID16348经原入口实际接受第34项fresh native readback，原plan／request／存档未换、下一工件35未覆盖旧序号；随后派发前因 `crop_teacher_run_day_advanced_without_verified_save_boundary` 退出2，四归属进程全退出。不是原生睡眠再次失败：原第32项日界0→1已接受，但新Main仅把旧transition ledger完成键传给原规划器，漏接托管owner记录。首次Block104调用路径空格未引用而在解析前失败亦保留，脚本修正不改旧参数校验。
- 新只读 `ReadPlanningProgress` 在同一owner锁内消费既有已验证接受证书的hash链，原日界只映射native overnight＋save已接受的from→to，原收获只映射已接受applied tiles，复用原DayBoundaryKey／HarvestTileKey。返回独立集合，不按当前日期／作物缺失推断、不把readback／控制算生产、不另建ledger；每次只读小证书，不重新深验全部历史快照，终局原完整trace证明不变。Main薄适配给原 `CropTeacherRunPlanner.Evaluate` 供键，非托管默认不变，原planner／旧owner索引／桥／执行器未修改。
- 控制端首次 **53/54**：唯一失败是新测试误调用旧seed-only `ReadStep()`，已改读原harvest admission，旧API保持拒绝非种子。最终同组 **54/54（1分4秒）**，覆盖日界与加载后接续、收获不重收、未接受日界仍阻塞、重开／pending／历史证书篡改／集合隔离和旧恢复／trace；不与前组相加，不称真实生产周期通过。
- 原APIBlock105同会话只读审查11steps未发现问题，未发现本次真实集成漏参；Block107初次因缺必需rg／gitlog停下，唯一修正因猜错依赖文件名按过严退出规则停下，均零代码／零测试。原输出／任务包／失败保留，控制端接管实现验收，不再支付第三次调用；权限已撤、授权仍在，此块blocked-idle不是provider限额。后续文件名错误应由已授权的窄rg纠正，不当成权限／provider故障。
- **唯一接续：** 提交后原S新日志目录隐藏静音重开，经同一owner新加载接受后继续原13日84动作维护→收获→销售→终端保存／内容读回；不回改Block106失败、不拼未保存尾部。原performance只读取样：106累计57.891更新／秒、仅2次full（平均2382.464ms），冷恢复约4分钟仍有代价，不称性能已解决。完整周期／Teacher总验收／formal仍false，local-data不提交。

## 2026-11-14 R2 同一 owner 显式生产续跑入口

- 新命令 `continue-crop-managed-owner` 复用原 readonly resume 的完整恢复／报告正文及原 Main 生产循环，普通入口和 `resume-crop-managed-owner` 默认不变。恢复同一 owner 的原 plan／request、当前账本和原 artifact 序号；派发前要求本次真实新进程加载被原验证器接受，并绑定该 PID／启动时间／可执行文件，进程退出即停止，不使用旧15724帧。
- 原13日／84生产动作、原 S 存档与输入身份不变。未另造 planner／executor／owner，也未改旧桥、资源读取器或动作合同。控制端组合 **59/59（1分3秒）**，含模式反例、进程寿命、元数据和原读回／恢复回归；原恢复报告正文机械比较与上一提交完全一致。这些是协议测试，不代表真实续跑或完整周期通过。
- 原API同会话Block102两次测试为0/6红色及编译失败，原因包含控制端误把已有 `Continuation.cs` 声明为新文件。原草稿／失败记录均保留；控制端恢复旧文件原内容，把新元数据移至 `RuntimeContinuationSource.cs` 并修复集成后通过上述59项，不将其写成peer验收绿。新增NEW路径预检在加载凭据前拒绝既存／已跟踪文件，离线7/7；不再支付第三次重试。7209ebdd CI43547548530成功。
- **唯一接续：** 提交后以同一 S 原存档隐藏静音启动新进程，经新显式入口接回原完整维护→收获→销售→终端保存／新进程读回链。运行脚本不覆盖旧调用／进程日志，以原只读performance接口取样，不额外轮询full快照；保留首次真实断点。完整周期／Teacher总验收／formal仍false，local-data及原生日志／存档不提交。协作授权继续有效，Block102进程已退出、权限已撤，下一块须替换任务包后才调用。

## 2026-11-14 R2 显式原生加载比较与读回接续

- `65205431` CI43543869020已成功。本轮沿既定R2第3／4步解决同一S存档的加载语义，不新增动作／字段／全量验收分母。原 `CropTeacherSavedResourceReadback.Open/VerifySnapshotContent` 默认严格行为、共享 `FullShipmentRecoverySettlementBoundaryProjector`、桥和执行器均保留；新增显式 `OpenNativeLoad/VerifyNativeLoadSnapshotContent`，仅由原生加载见证在验证进程、保存、日志、事件之后调用。
- 比较视图只处理已重新反编译确认的初始化：保留所有既有hay行，只移除新增且结构完整、名字规范、building／hay／capacity均为0的原生Cellar行；真实存量、容量、建筑、未知字段及重复身份仍拒绝。钱包先从原值完整推导每位参与者的individual初始化，核对全部其他属性，再将已验证字段映射回比较值；不忽略整个钱包，不复制旧投影器。派生fingerprint只在确有初始化时允许变化，单独变fingerprint仍拒绝。视图不落盘、不入快照缓存，不改原before／after或其hash，rebind仍使用真实after。
- 原API同会话Block97负责钱包函数／纯测试两文件，控制端并行负责Cellar、显式入口接线和完整消费者测试。peer `acb8fe80` 接为Main `188ebfee`；原工具红0/18（缺接口）→钱包＋旧读回28/28（2秒），仅两次测试，无修正／fallback／内部代理。控制端交叉审查后独立组合 **105/105（1分19秒）**，覆盖默认严格入口仍拒绝合法加载初始化、新显式入口／原marker rebind、钱包与Cellar反例、读回／owner／原overnight恢复。此处是合成协议测试，不独立授予真实加载信用；原始交流70条JSONL本地保存。
- 同一份今天上午S／15724真实加载的更正日志见证，交新原验证器完整只读回放，`NativeReloadVerified/SemanticSavedContentVerified=true`，10份原输入及当前原存档字节未变；未再开游戏／派发动作，尚未自行记owner接受。薄.NET8调用器先与Main四个依赖逐个核hash一致，排除旧DLL重放；旧失败及更正来源记录不改写。
- **实际owner接续已过。** 既有 `resume-crop-managed-owner --managed-crop-reload-observation <原更正见证>` 实际接受同一owner第33项，`native_saved_content_readback_verified/fresh_accepted_head_verified=true`；415份既有输入／原存档未变、无新游戏／native，`complete_managed_runtime_verified=false`。这是今天上午已真实发生的15724加载之留存结果入账，不称本轮新开一次游戏，也不回改Block92失败或Block94来源更正。后端／CLI均退出；原加载、存档和之前两轮失败证据继续保留。
- **后续仍是原R2第3／4步主循环接线：** 原只读resume命令不是生产循环；Main普通入口当前明确拒绝resume参数，不能把读回报告称作“已恢复行走”。须显式接回同一原owner／accepted head、原native run plan／request／账本，复用原 `NextArtifactIteration` 避免覆盖S旧快照；动作派发前绑定当前真实新进程加载见证，不拿已退出15724的帧直接执行。继续原13日／84生产动作的维护、收获、销售与终端保存读回，不新建owner／执行器／计划，旧普通入口和只读恢复默认不变。完整周期／Teacher总验收／formal仍false，#569正式manifest前交实验室及local-data不提交不变。

## 2026-11-14 R2 原生睡眠终端副本适配

- **S覆盖下方R历史接续。** 原E隔离 `managed-s-20261114-0840` 在原13日／84动作计划内，六粒播种和首次浇水全部被同owner接受，回家后iteration34原生睡眠applied／success=true；磁盘原档已为春季第2天。原loop退出2、四归属进程全退出，原因是新增Overnight消费者要求单步after与汇总after同文件名，非游戏睡觉失败。两原件内容SHA均为 `5ae3b8977389fd21db17e8a35238fcc261829cd1595a2150207b6a2b7d31c4a5`；原执行器正常写单步文件并另存终端汇总，不修改这套写法。
- 只适配新消费者：复用Harvest的完整typed快照比较／两路径留存模式，接受内容一致的终端副本，原路径、单步／run／actor／帧／跨日生长和编译合同保留。未更改旧桥、执行器、共享守卫或原回执，不逐项预测私有字段／随机结果。原API同会话草稿保留为 `5a5865b9`：首轮漏两条必需只读命令而零编辑退出；唯一权限修正后首测缺import未编译、第二次28/29且最后夹具修正未测，**不能称peer验收通过**。控制端新测试在旧消费者上4通过／5失败，修后原睡眠／收获／恢复／读回相关 **59/59（1分4秒）**。
- 原S留存准备→原完整Overnight范围只读重放，从同路径误拒转为通过：total day 0→1、六株生长、八输入均未变，两个物理after均被固定。这是范围重放，不自行补记owner接受。续接只走既有 `RunOvernightAsync` 处理已经claim的原结果，再核原生存档边界／原owner持久读回；绝不重发睡觉或修改原存档。薄调用工具最初两次在.NET10 PowerShell编译绑定处失败，均未启动后端／改账本，改用同版本.NET8调用原API，失败记录留存。
- **实际续接已过。** 原 `RunOvernightAsync` 给原S第32项生成真实accepted，`native_overnight_control_verified/native_save_boundary_verified=true`、六株day0→1；随后既有resume重开同owner并读回持久账本，404份既有输入／存档均未变、零新native。原失败回执与此前只读报告不改写。两次完整冷重开约7分钟，这是真实恢复代价，不混称native动作耗时或API等待；本轮不改旧索引／哈希缓存来规避检查。
- **新进程加载已实际发生，完整读回尚未接受。** Block92用同一原档／原smoke-mods／E可执行文件隐藏静音启动15724，原入口取得真实SaveLoaded事件、full加载快照和进程／保存指纹；调用工具误将UTF16控制台stdout当UTF8 SMAPI原日志，故原命令退出2。原失败保留。正确SMAPI文件确有根目录／槽位记录，最后写入01:46:33Z早于原观测01:46:37Z、晚于进程启动01:42:39Z；另建更正日志引用的证据描述，原快照／事件／进程／原描述hash均保留，不补写时间、重跑游戏或改原记录。还修本地调用器未设独立training-output的遗漏，未修改Harness。原验证器已越过日志检查，当前只拒绝 `farm.hay_storage,player.multiplayer_wallet`。原存档指纹仍等于加载前／后，全部归属进程已退出，readback owner接受／完整周期／formal仍false。
- **下一缺口有当前DLL依据，不是新增游戏能力。** 只读差异：loaded多出零building／hay／capacity的Cellar2…8；钱包同一actor／有效钱20不变，仅individual条目由缺失变20及派生fingerprint变化。对当前E DLL（SHA `7f1e5b8e…6deaee`／MVID `aa1f513a-94f0-4ef6-a35f-2d5b4a3721bf`）重新定点反编译Game1.AddLocations `0x06000b58`、SaveGame.getLoadEnumerator `0x0600143f` 与Game1.loadForNewGame，三方法零诊断：Load先取max(saved limit,MaxPlayers)，loadedGame入口建默认Cellar，随后逐farmer将individualMoney设为当前Money。完整原件／IL及源码hash仅本地留存，不提交游戏源码。
- **唯一接续：** 只修新增saved-load消费者对上述原生初始化的持久资源比较语义，复用旧资源投影，不修改共享读取器／同进程recovery守卫，不忽略整个hay／wallet字段；实际hay／容量／建筑／资金／钱包模式／参与者变化仍须拒绝。补精确正反例后先用同一份未改的更正加载证据验证，再经原owner入口接受读回；不为拿日志重跑游戏。只读resume本身不是生产主循环续跑，随后仍须完成原13日／84动作收获销售周期。9c828594 CI43535210356成功，新提交CI另核；#339/#550/#569、L1冻结及local-data不提交不变。

## 2026-11-14 R2 原播种机械批次身份接线

- **当前R覆盖下方Q／P接续。** 506f15a8的CI43529962446成功；R原新E managed/product实际27owner／24verified／六次耕地，accepted26／27已将前四粒真实播种归入同一owner，证实37a81982入口身份及此前来源／缓存修正已越过真实播种。iteration30最后两粒native均applied，最终快照有六株／所选种子0，但末粒1→0被新增播种消费者的空槽投影误拒，最后两粒**未获owner接受**。原native loop退出2，四归属进程3256／22768／6592／3144全退出；工具外壳0只是调用命令未显式exit原生退出码，不是游戏成功。摘要SHA`514bf102392590f7c0300e0fec9f5b02b0e2f216b956be5554ac4baa39a55b78`。尚无日界／收获销售／保存加载，原失败与未保存尾部不补写／续档。
- 根因是实际空槽的`donate_color_context`仍为非空不适用对象；末粒前后均为`is_colored_object=false／parent=null／tags=[]／not_applicable_not_colored_object`。核当前PlayerReadAdapter及4d93623d原投影，错误在a3869f97新增播种消费者把它预计为null；5cebff25出货投影已有正确做法。原API同会话Block87仅将原出货空槽函数原样移到公共Inventory partial并给播种复用，删除其重复白名单，peer`bf4f5038`接为Main`4a6e3146`。**旧桥、执行器、marker、prefix／资源比较／回执守卫均未放宽**，不是新增字段或逐私有状态模拟。
- peer原工具实证：新20项先18通过／2失败（拒绝合法对象、接受错误null），修后原播种／出货及新反例**109/109（13秒）**；仅两次明确测试，四文件提交，退出撤权。控制端逐文件复核后独立扩大**114/114（18秒）**，与109不相加。原R末粒的原queue／marker ledger／前后full快照经原**完整VerifyPlanting资源叶**只读检查，从同库存错误转为通过，五输入hash未变，报告SHA`5709d2331133c6c02fdcaeaabd314420a2973186ea646912c33f63a382df4ee2`；无native派发／ledger mutation／实际owner接受。初版因PowerShell对象装箱未进入原函数而失败留存，修工具后准确复现原错误，不将工具失败当游戏缺口。该重放不证明整个prefix／owner／完整作物闭环，不能补写R最后两粒信用。87原始交流68条JSONL及完整日志本地留存。
- **Q历史断点。** Q原新E managed/product运行实际接受25个owner边界／22verified／六次耕地／三次显式native站位移动；iteration28已准备原六粒种子队列，但在派发前被新增Main入口的`Managed crop seed queue lost its original selected candidate identity`拒绝。没有第26项dispatch claim／播种native，wrapper65723退出2、四归属进程9856／12520／2284／8全退出；日界／收获销售／原生保存加载仍未过。原件与旧失败保留，摘要SHA`84ce152df5cfa5a65b5740acc412a534bd74b816d494fa99375de82933ac4222`；不以通用objective_completed或未保存尾部冒充作物成功／续档。
- 核过`616a186b`原提交及同次交接记录：它将队列校验移入原owner执行租约，目的是保护所有权，不是要求直接机械批次改写为排名计划。原`CropTeacherSeedPlantingMarker.BuildActions/ReadQueue`按support step展开`:plant:index`并严格比对完整参数，本来无`candidate_id:`；新入口误把购种的候选约定套给播种。**37a81982只修新增Main适配器**：同一prepared runtime／owner／pending及原planting scope已绑定时，无排名ID使用既有`SupportStepId`作为该控制批次的回执身份。旧无主入口／其他种子路径的缺身份拒绝保留；原marker／动作／编译器／租约／资源核不改，不给原参数增加伪候选或授Teacher标签。
- 新主入口测试先复现同错误（1失败／1通过），修后相关**65/65（47秒）**。涵盖无排名参数的原机械队列不被改写、同一主执行入口／八步prefix／一次marker与settlement／owner接受，以及旧非托管拒绝和原播种恢复／来源刷新等；这是合成native协议测试，不是Q原件补写接受或真实播种PASS。
- Q只读performance取样（不额外生成快照）：full137次平均1327.587ms、locations403.398ms，仍有停顿。原API同会话Block84在Q运行时只写独立五文件草稿`610fda97`，控制端接为`b51c7b05`；同一次ShopAccess.Collect内以location／map引用共享原MapActionRow数组，跨采集即失效，原扫描／字段／排序、collision／动态gate／库存缓存与执行器不改。peer未构建／运行游戏，不称peer测试或性能已过。控制端组合首次85/87：两项旧播种fixture在FileStrategyCommitmentRepository.File.Move出现AccessDenied，其余（含新缓存9项）通过；不改共享仓库、不跳过断言，原产物不改源码的一次聚焦重放33/33（8秒），包括两失败theory及新缓存／原库存缓存／商店／连接器守卫。保留初败，具体外部文件锁原因未证实，不能把整组87重标为全绿；真实E引用net6 Bridge构建0警告／0错误。原始交流60条JSONL及完整日志本地留存，未用内部代理／fallback。
- 扩展旧route／social源码守卫首次6/7：`55dc054fe`旧守卫用整个`Game1.warpFarmer`文本断言，命中了基线fae6a461已经存在的`"Game1.warpFarmer active-festival branch"`来源说明，并非本轮添加调用。只将该守卫改成识别函数调用语法，补说明文字与三类实际调用反例；未改GameLocation、gate或执行器。最终相关**11/11（41毫秒）**，初败留存；这只是源码回归，不外推为完整游戏只读证明。
- R早期只读performance full94次平均1171.142ms／locations360.898ms；相比Q的1327.587／403.398ms数值较低，但不同阶段负载，不作为受控因果比较或“卡顿修好”。只读旧13MB工件的Net10宿主取样显示反序列化／canonical hash仍有秒级成本；该宿主和CPU争用不同，文本作用域复用不是typed对象安全复用证明，不据此擅改共享哈希／旧读取器或新增准入门。
- **唯一接续：** 空槽共享修正组合验收／原R资源重放／提交同步后，新S在E隔离原managed/product入口继续真实播种维护→收获销售→原生保存／新进程加载；原计划13日／84生产动作，不能把整地或四粒入账缩成闭环成功。不重发Q／R批次来伪造内存续档，不再为原机械批次新增排名前置或私有字段预测门。性能优化不是新的动作准入门；L1冻结19/19、11/11，formal=false、PR567草稿、#339/#550/#569分工与local-data不提交不变。

## 2026-11-14 R2 同tick库存图缓存修正

- `dc5df200`已推送，CI43510625527成功；M原source重新导入的修复及12/12／只读HTTP证据继续有效。新N原入口接受5个owner边界，iteration7购种队列在第4步close_menu原生applied／verified后被资源核拒绝；loop63076实际退出2，四归属进程3024／4028／2080／17896已退出。
- 已核原始逐步快照：tick10461购前money420／背包种子1／库存图1，购后同tick money340／背包2／库存图仍1；tick10462关菜单后money340／背包2／库存图2。不是关菜单凭空增添物品，而是透明桥按tick复用的图滞后。三原件未改，诊断报告SHA `357bf7bfc7e594564ee8738b7770829c5dde105abf579964a13a3f7ecde79413`。N旧证据仍失败，不补写／重标成功。
- 旧缓存由`f3f046f7`为current-location／farm共用规范库存图引入，保留这个目的：原`ReadCachedMaterialInventoryGraph`委托单次采集作用域缓存，`TransparentStateCollector.BuildSnapshot`以using进入／释放；同一次采集同farm／actor／tick只读一次，新采集即便同tick也重读，非采集调用不跨请求复用，嵌套／异常恢复作用域。未增加快照频率、关闭其他缓存、修改旧购买／关菜单执行器或资源核。
- 控制端首次新测试因Assert.Throws重载歧义未编译，仅显式Action修正。最终缓存／三类种子入口／原transfer owner／profile及原graph守卫**25/25（2秒）**；真实E引用的net6 Bridge构建零警告／错误。另补跑旧Core源码守卫，19/20的一项旧字面量落后于`7da69574`共用materialInventoryField改写，仅改守卫为同时验证Field构造和原输出绑定，旧含义不放松；重新正常构建后该组**20/20（110毫秒）**，不把初次红色重标。
- 原API Block77首轮因控制端给不存在的独立record文件而7steps零编辑退出；核实私有record实际在Program.CropTeacherControl.cs:13，沿原会话唯一修正后13steps交两份测试草稿`049901ac`，接为`606defdf`。控制端23/25发现新夹具缺sleep context／完整envelope及准备阶段错误调用执行租约API，只修新测试夹具／断言，最终25项通过。草稿／失败／完整API流均保留，不称peer首次验收绿；现accepted-idle／撤权，未替换内部代理或fallback。
- **O真实结果（当前）：** `622ef4b6`已推送、CI43514002736成功。O原入口真实接受24个owner边界／21verified／六次耕地，已越过N购买失败；iteration26在随后接近播种站位72,17时原生move_to_tile applied／verified（12ticks），但旧中性路线核拒绝。原after25 tick12375有1个debris、背包无黏土；after26 tick12391该debris为0、slot7新增`(O)330`×1，六粒作物种子不变；所列prize／tool／quest字段均是库存变化的派生投影，不另加白名单模拟。O包装器55219退出2、四进程23520／16368／18416／14824全退出；首次日界／播种／收获销售／原生保存加载均未通过，旧失败不改写。
- O五份原顶层输入经现有`NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue`只读重放通过：原queue／request journal／Product授权／run／actor／帧与真实after绑定有效，before／after hash `194d9151…7f93a8e`→`6f71192b…23035cb`，报告SHA `d3688842d45edab62d0eb453432cc87fd9f4a1b13c0ccbb4515e476fdceefb02`；无新派发／ledger mutation／实际owner接受。原移动result的target_location为空、target_tile为null是既有形态，后续核实际after位置和原编译站位，不新增要求这些回执字段才准入。
- 同轮已有只读performance接口取样：full122次、平均1277.992ms／最大3595.648ms，locations平均402.59ms、player156.221ms、farm4.711ms，累计更新52.367/s；这是仍有停顿，不称性能解决。未额外生成快照或扩缓存。后续优化以原指标定位重复采集／序列化，保持字段／hash／原生绑定，不恢复跨采集tick缓存、不默认删字段；不将性能工作另造为本次动作证明门。
- 显式`OpenNativeSiteApproach`已实现，准备／原模型／原编译绑定不变，默认`OpenSiteApproach`仍走原中性资源核；新结果委托同一`CropTeacherSiteNativeTransition`，只核原动作与真实after的精确站位，不要求旧move回执新增target字段。旧／新模式及相关clear／till／owner回归**49/49（14秒）**。O未改原件经新公开准备→同一原生协议→精确72,17→原rebind请求只读重放通过，报告SHA `6f1d5c0f3f493bde36c0643ec767ef89cbb69aef059d7ea6da9395a4ad08c916`；留存输入未变，没有新派发／ledger mutation／实际owner接受。模式尚未接主入口／持久化，不能称本轮实跑修复通过。
- 原API Block81已将显式模式接原owner admission／supporting intent／重开恢复，Product主入口选择true，缺字段旧记录及显式false仍为原中性route；拒绝非boolean／错角色／native-control组合，不从当前live选项反推旧模式。结果／acceptance绑定`native_site_approach_effects_trusted`，不授crop／Teacher／formal信用。peer原测试一次**95/95（23秒）**；最后合并多条Git命令被精确白名单拒绝，控制端检查四份变更、stage后diff-check、提交保存为peer`e3257369`／Main`3414f371`，未改其代码或付费重试。控制端独立扩大组合**113/113（31秒）**通过，不与95或49相加；尚无正向native owner outcome／acceptance恢复fixture，真实P必须补这段证据。
- 已查后续生产证据组装：取得所有原accepted历史／hash后，从首个真实planting起进入原作物生命周期分母；本次播种前站位拾取不会在终端再按中性route重验。生产区间内转换不删除／重标。下一原生监护单轮有界上限3600秒（原1800不足以给生产段留裕量），仅本地监督预算／invocation元数据变化，不改变游戏速度、动作超时或验收标准。
- **P实跑（当前）：** c7864265原新E运行实际接受18个owner边界／15verified，accepted17明确`native_site_approach_verified=true`／`native_site_approach_effects_trusted=true`并到73,17，随后accepted18原耕地通过。iteration19因磁盘保护正常停止：E可用8585445376bytes，低于默认8589934592bytes约4.28MiB；不是动作异常，stderr为空、四归属进程14788／12404／14052／3532已退出，包装器73750退出2。原P同owner经原`resume-crop-managed-owner`只读重开通过，241份既有owner／head输入未变、零新native；报告仍明确saved-content-readback／complete-runtime／formal=false，不能称新游戏加载或续档。摘要SHA `627b65b27820742fcfdf19b73031a1b306e6abe4a0c694b31290efffe15139d9`。带拾取的O公开协议重放与P实际mode入账／只读恢复分开陈述，不声称P发生同一黏土拾取。
- M／N／O／P目录只读统计分别137.34／80.64／134.03／104.37MiB。未清理／移动任何旧证据。下一轮通过既有`--min-free-space-mb 6144`显式预留6GiB，当前约8GiB留下约2GiB写入余量，仍逐轮检查保护；应用与本地调用器未传参默认均8192MiB，不改项目默认值、不禁用保护。调用器参数传递／语法已核对，原脚本归档hash核对；60分钟监护／游戏速率／动作超时／成功标准不变。
- **唯一接续：** 新Q显式6GiB保留预算，原E managed/product继续实际站位移动／连续整地→播种维护→收获销售→原生保存／新进程加载。P补上真实显式模式入账及原只读恢复，但未播种／日界／收获／销售／实际保存读回；不能用其未保存尾部或O新模式离线重放补写旧接受，不能称M缓存修复已获实际播种PASS。L／M／N／O／P未保存尾部都不是续档。不新增执行器、私有字段证明或字段白名单。L1冻结19/19、11/11；formal=false、PR567草稿、#339/#550/#569分工和local-data不提交不变。

## 2026-11-14 R2 原生动作信任边界修正

- 用户指出：原生割草不应以逐字段模拟私有状态、随机掉落或邻格清除为前置门。接受这一修正；此前B校准的镰刀动作已成功，后续拒绝来自新增消费者不接受正常工具XML变化，并非发现原执行器乱删或作弊。**撤销下方历史接续中“先完成Scythe私有元数据／weed范围／所有未变资源证明，才准许整地清障”的要求**；历史失败和原件保留，不改写为成功。
- 保留严格边界：原候选与首个frontier、编译参数、原生成功回执、同run／存档／actor／帧、完整真实after、一次claim及原同owner账本续接／恢复。正常原生副作用由游戏引擎裁决并记录，不独立预测全部XML；准备动作不直接授予生产／Teacher／formal信用。调试夹具仍不得作为正式产出。旧默认读取器与已过耕地资源合同不放宽、不增加默认扫描。
- `74c28522`抽取原耕地回执／留存／rebind为同一共享协议，旧耕地仍调用原资源核；清障显式消费原生结果与真实after，无第二套执行器。聚焦44/44通过，首次新代码误用不存在的request.TargetLocation导致编译失败已修；该失败留本地。尚未完成真实owner／整地生产保存加载，不称完整循环通过。
- 原API Block72在漏配rg权限后7steps退出、零编辑／测试；尚未启动的补跑已取消，旧任务／三份未编译草稿归档并hash核对。Block73沿原provider／模型／session实际协作：peer负责同owner清障注册及限定测试，控制端负责Bootstrap、Program入口、原生结果测试和集成；单一build owner、输出留本地供审查，不倒灌完整流。
- Block73已交付12文件草稿，项目全部编译；其测试工具120秒终止、无最终计数，不能称peer验收通过。确认无遗留测试进程后，草稿完整保存在peer `e42f7e99`、接为`29319218`；控制端补原入口与原生协议组合测试，最终**124/124（52秒）**及旧route／watering／control共享owner抽样**6/6（3秒）**。覆盖clear／access、训练／Product回执、正常私有／邻格副作用、身份拒绝、同owner正常／native回包丢失／marker回包丢失、一次native／一次commit及trace；均为合成协议测试，不冒称游戏实跑。控制端首次测试缺Testing导入的编译失败留存，仅补新测试导入。
- `b0e58677`已推送，CI43497118254全部成功。新E隔离原入口 `managed-l-20261114-0352` 实际接受**25个owner边界／22条verified action／六次耕地**，到iteration28播种准入前停在`crop_seed_planting_source_binding_invalid`／`dispatch_crop_seed_planting_binding_drifted`。该档障碍分布不同，**未遇清障，不能算清障实际owner通过**；中途控制端曾沿用K迭代位置误报“即将清障”，已按本轮模型／回执纠正。四归属进程全部退出；首次日界／收获／销售／原生保存加载均未过。报告`objective_completed=true`仅表示通用`required_verified_actions=1`采样额度，不是作物闭环成功。
- 播种断点是准备层遗漏：账本头随中性整地更新，但六粒自有种子的预留仍指购入时source hash。保留原编译器／`CropSeedPlantingBinding`的同源要求，通过原portfolio marker／MaterialReservationUpsert仅刷新当前承诺已核物理玩家种子预留，保留reservation／来源decision／槽位／品种／数量／purpose；已当前的预留仍走原空marker，其他共享调用／执行器不改。红测1失败／1通过；生产修正的相关265项通过，新回归初版漏补既有growth夹具而失败，复用原helper后关键新旧**3/3**，不宣称同一次266/266。
- L原accepted25（tick12395／hash`b75f2cfc7fd3a7d290d85f27655f8c42e14da09caf8e56d3f638d14eb9fdfbe4`）、原账本／原六格计划只读重放原编译与growth预检通过：仅一条六粒预留刷新，六输入不变、三实际加载依赖hash留本地；报告SHA`55eea88ca49e945d0143f6bf344b9b0b1cabe936b618a3a8cbd11cdc09a09790`。**没有新native派发或实际播种owner准入**，不改写L旧失败，也不拿其未保存内存态伪造续档。
- f8dbe970的CI43501813625全部成功。新M `managed-m-20261114-0433` 再接受25边界／22verified／六次耕地，iteration28已通过来源预检并持久化第26项播种intent／准备claim，随后读取账本时HTTP404，尚未编译／派发播种。Backend原`commitments/latest`明确在匹配快照被有界缓存淘汰时返回404；种子父入口漏了其他owner路径已有的精确来源重新导入。只在共同种子入口admit前pin原source／ledger并复用原`IngestCropTeacherManagedSnapshotAsync`，不扩大缓存、fresh替换来源、改账本或执行器。
- 缓存淘汰入口回归已按同一HTTP404／调用链复现，修正后连同来源刷新／HTTP正常与丢回包／原事务及owner选取共**12/12（8秒）**。新缓存测试初版误用internal成员、随后漏绝对测试URL均只修新测试，失败留本地。API Block75在M实跑时仅编辑一份75行HTTP草稿、无编译或运行权限，15steps后退出撤权；`e524f724`保留、接为`8252736d`。控制端首次6/8，新两例把预制回执误当实际派发回执来断言重复完成，已只修测试调用并通过最终12项，旧原生前缀保护不改，不冒称peer首次全绿。
- M四归属进程已退出，实际loop／外层脚本现均退出2；本地包装器已归档旧版并保留原生退出码，避免外壳0被误当成功。M未遇清障，也没有实际播种／日界／收获销售／保存加载。`objective_completed=true`仍只是通用采样额度，不是本目标。API授权保留idle，会话／完整输出／草稿均留存，未调用fallback。
- M原accepted25／source `40f21bdc…1dbeeac2`／原ledger经独立原Backend HTTP检查：初始缓存404→原Program精确导入→整个原账本DeepEquals，三输入未变；无游戏／native／ledger mutation／实际owner准入，报告SHA `3d4b56c93ed5d5d9e819b0aa14fa79f5916727db8e4551dd15012af0b293a3b1`。本地工具首次直接访问internal类型的编译失败仅改反射调用，项目接口未变；不能把此只读HTTP证据当完整准备或作物PASS。
- **唯一接续：** 带精确来源重新导入修正的新E隔离原managed/product入口，继续播种维护→收获销售→原生保存／新进程加载。原协议组件12/12及M留存HTTP检查已过，不为同一缓存错误再建验证框架。清障遇到即走原生信任入口，不增加私有XML／weed范围证明门。L／M未保存尾部不可伪装续档，原恢复接口和一次claim规则保留。L1冻结19/19、11/11及#569正式manifest前交实验室不变；formal=false、PR567草稿、local-data不提交。

## 2026-11-14T02:15:00+08:00 R2 原首选杂草清障编译与资源断点

- 修正版新E隔离 `managed-k-20261114-0052` 原managed/product循环接受19个边界、两次耕地。iteration21原首选是 `clear:Farm:70,17:weeds`／`(O)784`，站位71,17、整地父目标70,18；原保护在派发前拒绝尚未绑定资源效果的整地清障，未生成该动作的owner intent／claim／acceptance。不是南向耕地再次失败，不筛掉该首选去做后续till。原K已退出，未保存尾部不能当续档；`4e72d0cd` CI43466162896成功。
- 从已有till准备组件提取同一原request／plan／观测／ledger／whole-model／原编译器绑定，till仍委托同一实现并保留到站位／Hoe／精力核。新增**只读**clear准备，显式区分整地父目标、实际清障目标与站位，不接派发／owner或放宽旧资源核。原API六文件草稿完整保存在本地`bfd38516`；其首次77/96，新夹具缺terrain_features、误写碰撞字段，控制端修后94/96，再把错误的固定13输入断言改成实际依赖路径集合，最终**96/96（44秒）**。旧失败留存，不把草稿退出码0写成验收通过。
- K未改原件经原共享配方为accepted full源另建模型、原编译器及新准备组件重放通过，13输入不变，source／窄观测hash分别保留。报告SHA `d735f48b4ea746de97f9799fde72fc14d04fcb5ca17b7efe9409d2abf0522d76`，三实际加载依赖hash在本地报告；**native／resource／actual owner／formal均false**。本地probe首次误用internal序列化入口、随后误把内容寻址manifest当快照的失败均保留；最终复用原内容寻址读取器并校验实际snapshot身份，未改项目读取合同。
- 同一E原版SHA／MVID实时提取7个精确MethodDef、零诊断，另记录MeleeWeapon未声明DoFunction、实际继承Tool。`cutWeed`有随机纤维／种子及条件掉落，`DoDamage`遍历范围内物体，不能先假设仅目标删除或固定掉落。新增显式weeds debug夹具复用旧镰刀执行器，未改其动画／动作。短校准A原生成功，但控制端漏填原合同的`source_planning_option_id`，锚点正确拒绝；按`ecb8fab0`及对话JSON确认原保护意图，只修本地调用，A留存、不重发、不计资源通过。
- 独立新档B `weed-resource-b-20261114-0224` 实际23ticks完成，scope／同帧缓存有效、零诊断，tick664→688，270→270；无Axe／Pickaxe调用、无拾取，after有2个地面掉落实体。原`ReadClearanceConsumption`严格重放通过，剩余持久差异仅`farm.objects`：原生范围动作同时移除71,16和71,17两棵`(O)784`。分析报告SHA `8024f2b7039c9bbc7268e17103eea908e27d7fe6afe7fb8a3200f37f4efc4a80`，原件未变、3依赖hash留存；**完整资源／生产归因／owner／Teacher／formal仍false**。两档都仅debug校准，全部归属游戏进程已退出，不代表K清障或managed闭环通过。
- `626752ca`已推送；其CI43482684774在Core2606/2608处发现两项源码守卫未同步新增weeds白名单。已保留旧防直接清障／掉落／经验修改的全部断言，更新名单并增加原版`(O)784`／IsWeeds／Scythe及透明目标检查，控制端聚焦**3/3**通过；仅测试修正，不重跑游戏或修改执行器。新提交CI另核，不能以96/96或旧绿色结果代替。
- **唯一接续：** 复用原消耗／数量／库存图核，对所选Scythe／Weeds的实际`farm.objects`范围删除建立最小严格比较叶，绑定原生范围与未变的其他对象／持久资源；再接原同owner claim／rebind／恢复。未支持效果继续派发前拒绝，不先删保护或另建执行器。随后才新隔离原入口完成整地、播种／维护／收获／销售／原生保存／新进程加载。#569正式manifest前交实验室、L1冻结19/19／11/11、formal=false、PR567草稿及local-data不提交不变。[详细证据](refill-approach-native-trace.md#2026-11-14-native-k-site-clearance-preparation)。

## 2026-11-14T00:46:00+08:00 R2 新J连续耕地与南向完成态修正

- 新E隔离 `managed-j-20261114-0016` 走原managed/product主循环，20个边界接受，其中耕地71,16／70,17／71,17连续三次自动入账、各pickup0；accepted18→19→20及pending21的前驱哈希、三个after快照／ledger哈希已独立核对。第二次未漂移回执现正常接受，I20暴露的replan错误已获新实跑验证；`f61b5dff` CI43460584032成功。
- J22第四次耕地70,18原生成功，新增元数据预计函数却把南向终态算为initial+1，实际为initial。已按提交`47b8e254`／peer原`2157a195`查回本对话JSON：当时是限定纯数值模型／27项测试，未作南向原生验证，不是旧执行器。当前E原版SHA／MVID不变，重新实时反编译21个精确MethodDef、零诊断：结束帧回调解锁后，`doneWithAnimation→Farmer.Halt→Character.Halt→FarmerSprite.StopAnimation`在南向重置animation index，随后同次`animateOnce→Tool.Update(2,0)`恢复initial。旧推导漏了这个退出链，不能用最后动画帧替代idle终态。
- 只修新增纯预计函数南向offset1→0，保留N/E/W和swing／history规则；未改旧执行器／动画、未增加观察字段／全局扫描，也未接受任意贴图范围。新增完整消费者的独立字面量21正例和伪造22反例，非仅用被测公式生成预期。最终相关 **292/292（48秒）**，零失败／跳过／最终构建警告／错误，旧55属于前次replan修正版，不相加。
- J22原件经既有通用probe完整资源＋进程内rebind通过，20输入未变，after tick12254/hash `b75e5f6996769bcd4d2f0eb0a78438f8d28ee0429951722f15eac7ea7cf52e31`、pickup0；报告SHA `34542ef33ef5112403a5f5aaf96ed9d00496c554b8916d447c8b4fcafc9e0ce0`。本地另存实际加载的Contracts／Core／Bootstrap三依赖的内容寻址hash清单（SHA `eb59546bc3f7ac51deeb220c125e97266a8f53f1416840b293734db14852c344`），避免只pin上层DLL而漏Core预计函数。**J22实际owner接受仍false**，不改写J旧失败，不重发native；四归属进程全部退出。
- **唯一接续：** 修正版新E隔离原managed/product运行，继续连续整地至播种／维护／收获／销售／原生保存／新进程加载。已有三耕入账和原回执重放不代表完整周期；H／I／J未保存内存态均不可当存档。#569分工、L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、local-data不提交不变，新提交CI另核。

## 2026-11-14T00:12:00+08:00 R2 新I原生耕地入账及无漂移Product修正

- 新E隔离 `managed-i-20261113-2356` 由原 managed/product 主循环真实接受18个边界，包含首个耕地71,16，`native_site_till_verified=true`、pickup0；accepted SHA `05ff504347e2840103035bc07416c351ea3ce53cdbfb478cf0a00222f74c81da`，after hash `9e17dc297417b403685935b0852a6df4809898c1bafa035b499c7a08460eb30d`。这补上了新入口实际派发→同owner入账，不只是H事后续接或fixture。
- 第二次耕地70,17已原生 applied／verified／observed、零诊断，却被**新消费者**要求 `product_replan_required=true` 的常量拒绝。旧 `ProductExecutionService` 的既有公式是“状态漂移或未applied或未verified才replan”；该次请求与fresh-before hash一致，drift=false／replan=false正确。仅改新适配器按已要求applied／verified后的漂移值核replan，旧服务／执行器／默认门不改；夹具同步去除错误常量，新增有／无漂移正向和两种矛盾标记反例。不得从一次样本值推导整个原协议。
- 最终相关 **55/55（33秒）**，覆盖Product边界、原12种owner恢复及旧Product服务；零失败／跳过／最终构建警告／错误。复用原通用只读probe打开I20未改原件，完整资源核及进程内rebind通过，20输入及verifier hash复核不变，报告SHA `255a93c6a7019cc5cadf9eb27fec81793a1bc02d2ba53a4723619c139c82f967`；after tick12150/hash `5012f33e85ac743aa78dd0c7865a344be2fc82b7e8ca4bade54bc90615d1a23a`、pickup0。**I20尚未实际owner接受**，没有新native派发，不改写旧失败。
- I的四个归属进程全部退出，原生保存／完整生产／销售／新进程加载仍未过；H持久完成记录继续有效。`bd483aba` CI43456855166成功。核对公开issue未发现比#569更新的外部任务；其正式manifest前实验室交接继续生效。R2正文两处旧“尚未接线”已改为当前实现／待原生验收，不重新开发已有模块或增加准入门。
- **唯一接续：** 用修正代码的新E隔离原managed/product运行，验证连续整地、播种／维护／收获／销售／原生保存及新进程加载；不重发I20，不把H或I未保存内存态当存档。优先首个真实阻断，不重采已过L1／旧恢复矩阵。L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、local-data不提交不变。

## 2026-11-13T23:50:00+08:00 R2 原H耕地实际入账与持久读回

- 原H已知成功回执通过既有 `RunTillingAsync` 明确续接，原 Backend 持久账本 revision **35→36**、同 owner 生成第18个 accepted；关闭并重开 owner 后，原恢复入口读回相同 ledger／state hash。只提交原 marker，不调用执行器，原 native 派发次数仍为1。20份原输入、accepted 哈希及4份本地内容寻址 verifier 归档已复核；原失败报告和原 loop 的17 accepted／14 verified统计不改写。
- 完成报告 SHA `4329dc7b597166a789e33359fcae8dd098df7cb66523dfc9100bc704dc755939`，`actual_owner_acceptance=true`、持久账本／重开读回均 true；after 仍 tick12091/hash `eea16ffd732df180e7a13bcd967a6b48a05ef420265e6ca20d6f120740367e75`。**没有原生保存／新游戏进程重载或完整作物周期**，旧只读重放报告的 false 是当时事实，不回改成成功。
- 原 API 协作者 `b2b76a70` 经逐行审查接为 `c8dcbfb9`：在原恢复 theory 增加 Product 参数，6种场景各 native／Product 两种，共12项；保留所有一次 native／一次 commit／重复只读恢复／无生产信用断言，没有复制实现或改生产、fixture。协作者首次12/12、控制端独立12/12（21秒），无失败／跳过；进程退出0、权限撤回，授权保留 idle。此前 `108748f4` 的 CI43452644390 已成功。
- 最新公开 issue 核对至 #569：正式 manifest 前交前瞻实验室已按原计划落地，主线不接管D盘模型定型／正式训练。**唯一接续：** 新E隔离档走原 managed/product 入口，验证耕地后播种／维护／原生睡眠／收获／销售及原生保存／新进程加载；H未保存内存态不得伪装可加载存档，不重发H19。遇首个真实阻断保留原件并修最小接线。L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、local-data不提交不变。

## 2026-11-13T23:25:00+08:00 R2 耕地同owner接线与真实Product断点修正

- `4e96a0d1`接独立`site_till`角色，共用原supporting准备／回执／一次native claim／marker提交与重放、索引／恢复／trace，不新建执行器。保留原窄观测／request／plan／模型，用共享完整配方和原编译器绑定accepted full源；其他未支持整地资源仍拒绝。深层观察仅启用在该次执行的私有options副本，原options、旧方法签名及全局默认不变。
- 合成原协议正向／篡改8项、六种同owner正常／丢回包／重开恢复均过，每种native1、commit1，重复读回不重发；未知claim仍pending。主入口聚焦147/147，前版共享owner回归1002/1002（4分36秒）。API59两次测试因缺SnapshotHash导入均未编译；草稿完整保存在`bed2e113`，控制端补导入和夹具物理图policy／count后验证，无第三次API／fallback。27个step_finish／短报告本地留存，退出撤权、授权idle，费用未知。
- E新独立档H `managed-h-20261113-2256`沿原managed/product入口后台隐藏静音，无debug资源布置。17个accepted边界／原报告14个verified action；H19真实`till:Farm:71,16`已applied／primitive verified／observed、零诊断，**owner尚未接受**。首断点为新消费者误用原直连native专用入口，而实际journal是`/api/v1/product/execute`。原`5bf13fbc/#539`限制是有意设计，不是旧执行器bug。
- 新所选till显式Product入口复用同一个ReadQueue／ReadFrame核，默认仍直连native；额外绑定Product schema／authorized、run+nonce receipt ID、journal canonical request hash、请求before、fresh before tick范围／drift、精确after、dispatch guard／replan和非replay。旧默认仍拒绝同一Product endpoint。12个Product正反例通过；最终当前代码相关**698/698（1分4秒）**，168／147是重叠子集，1002属于前一同owner版、不累加。零最终失败／跳过／构建警告／错误。
- H原件经修正入口完整资源＋原进程内rebind重放通过，实际pickup0；after tick12091/hash`eea16ffd732df180e7a13bcd967a6b48a05ef420265e6ca20d6f120740367e75`。报告SHA`d63fdcc22a9614768aa7b1621932f7b92d3deefcf624289fd1d59a241083fc6e`，20输入及verifier hash复核不变；**actual_owner_acceptance=false**，没有补写H旧回执／账本或重发native。四个归属进程均已退出；重复Stop碰到已退出PID的本地脚本竞态已修、原失败保留，无用户进程／存档受影响。[详细证据边界](refill-approach-native-trace.md#2026-11-13-same-owner-till-and-product-transport)。
- **唯一接续：** 核原Backend持久账本与存档边界，对H已知已执行原回执走既有明确续接／同owner marker提交和持久读回；不让只读resume创建未claim变更，不重发H19。条件不满足时保留拒绝、用新隔离档复验，再继续播种／维护／收获／销售／原生保存／新进程加载。完整链未过，不能把组件重放当闭环；L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、正式manifest前交实验室及local-data不提交不变。此前bb7a097e的CI43442221839已成功，新提交另验。

## 2026-11-13T22:22:00+08:00 R2 所选耕地物理资源组合通过

- 最新issue已复核：#569正式manifest前交实验室的责任分工已在本库落地，不改变当前R2第3／4步，不操作D盘或开训。`da5ecfc8`的CI43437873724已成功；不拿旧CI代表本轮新提交。
- 新`ReadTillingResources`组合同次显式原生till的槽位XML／deep graph／debris／数量证据、原精力／地形／统计叶，以及**原物理库存图、容量、房屋材料镜像核**。旧拾取入口仅提取同一私有核心，默认comparison为null、原规则不变；物理校验仍对原始before／after，只有最终持久不变量使用已验证比较视图，同时保留GSQ和house归一化。未知XML／其他资源或进度仍拒绝；实际库存增量不是生产归因或奖励credit。
- 原API同provider／模型／session的`c8f56c89`全部审查后接入`424b6250`：21/21首次通过、零修正，覆盖原物理拾取旧栈／新槽／新quantity种类、图／外部库存／容量／房屋／未知持久变化拒绝、原件不变。**有拾取正例是组合叶测试，完整public正例仅零拾取**，不混称原生全分支通过。进程退出0、权限撤回、授权accepted-idle，18个step_finish原字段与短报告本地留存，费用／余额未知。
- 控制端将未改动的原生F留存输入交新完整public资源入口，通过工具／精力／地形统计＋物理图／镜像组合，仍pickup0、旧入口仍拒绝；7输入及最终verifier DLL hash复核不变。报告SHA`eebc9750eddbb438529c6d395f575079fd206d82ecaf3f6219d97b2e9b4a320f`，仅debug校准来源，不是G19派发、owner入账或作物生产。最终相关**Backend559/559（17秒）**，聚焦252是子集不累计；零失败／跳过／最终构建警告／错误。首轮新测试缺Contracts.Training导入导致编译失败，已修、原失败保留。
- 原`CropTeacherSiteTillCompilation`现实现既有supporting准备接口、返回账本副本；新单次`CropTeacherSiteTilling`复用原队列／逐帧回执读取、nonce／journal／gzip、原生步骤与上述资源核，再交原`CropTeacherSupportingRebind`，不执行或提交。10项接口／角色／绝对路径／输入输出别名测试通过；**该消费者尚无正向原生／rebind验收，也未接owner派发与恢复**，不能移除G19保护。
- **唯一接续：** 在既定同owner接线中验证消费者正向及保留原件篡改反例，接原owner admission／claim／rebind／只读恢复与显式till观察参数，然后新E隔离managed实跑。复用原编译器／执行器／资源核，不重做已过元数据或扩大为全掉落枚举。完整生产／销售／原生保存／新进程加载尚未通过；L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、local-data不提交不变。

## 2026-11-13T21:53:00+08:00 R2 耕地工具XML绑定与新原生重放通过

- 原生字段引用／IL操作数定位后，实时核对`Farmer.Update 0x06000953 → Equip 0x06000941`：原`_lastEquippedTool`与当前tool引用不同时才装备并设置HasBeenInInventory。工具索引setter本身不设置该标志。`endUsing`的swingTicker恰好+1；begin／Update／动画tick／原生动画表共同决定结束贴图，N/E/S/W偏移为4/2/1/2。原生请求传8帧参数，但动画表实际构造5帧，已逐项核对，不能把参数当实际长度或一律+2。反编译原件／方法hash留本地，不提交游戏源码。
- API同provider／模型／session实现纯元数据函数，`2157a195`审查接入`47b8e254`，首次27/27，无修正；只处理显式输入／unchecked整型回绕及已核实Equip bool，不读游戏或授credit。控制端并行完成目的限定raw上下文：仅显式till观察读实际private last-equipped引用、actor／Hoe引用／槽、power／idle动画状态及原生字段，旧请求不扫描也不增加wire字段，缺schema／输入不能默认。
- 新`till`消费者把同一原生成功回执／精确锚点、原graph／transient／debris／库存数量／XML与上下文绑定，额外核after actor facing；只为已验证的那个Hoe构造三字段**期望XML**，仍走原槽位比较核。未知XML／其他槽／属性不抹除，原件／hash不改；旧拾取入口默认投影为null，仍拒绝相同工具变化，不是改旧执行器或放宽全局validator。
- E新隔离校准F使用原执行器／精确缓存读取：full before tick664/hash `a67a19aeed456646281ab9e73f462ffecb7bd6c278e9f1c9c63a1a248e8d1c29`，窄观测hash `f51e3dcbce44481ba92bb7ac58920dde184b35ff9bf250e9441a60a90d108862`；after tick701/hash `6f959c5a2d6794416722aa597160b93bf79d0d04f4fc8aca6c3e61d9dc6144d3`。实际context before665为Axe／slot0，after为同Hoe／slot1；facing1、power0、idle均明确，history false→true、sprite21→23、swing96226→96227符合原生链。新工具XML核及原精力／碗／地形／统计／无额外资源组合核通过，旧拾取入口仍拒绝。报告SHA `4c7918bb233236e4dc501442334a43237f9f77ee7e3044f24facd50148900901`，7输入及最终verifier DLL hash复核未变；实际pickup0，仅**该次无额外资源分支**，不是所有掉落分支／G19／owner／作物／Teacher／formal PASS。debug setup仅校准，游戏已停。
- 最终同代码相关 **Backend528/528（17秒）＋Core69/69（不到1秒）**，零失败／跳过／最终产品构建警告／错误；原262／263／51为子集不累计。覆盖原库存／物理拾取／graph／debris、工具／精力、准备及harvest／watering回归；新反例包括schema／引用／装备来源／动画／回执身份／锚点／额外XML，以及同时伪造facing和XML仍被原actor快照拒绝。首轮仅新caller一处nullable warning已修，原件留存。API17个step_finish及短报告已复制hash核，退出撤权、授权idle，费用／余额未知，无新provider／内部代理。
- **唯一接续：** 将同一次till已采集的库存／掉落通过原物理资源图与镜像核组合，保持生产归因／奖励credit独立；未知副作用仍拒绝，不重新枚举／重写已过动作。随后复用现有准备和同owner supporting协议接claim／rebind／恢复，才新managed实跑。当前G19保护仍在，完整生产／销售／原生保存／新游戏进程加载尚未通过；#569最新责任归属、L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放、local-data不提交不变。前一6adcf82a CI43429491093已成功，新提交CI另验。

## 2026-11-13T20:56:00+08:00 R2 显式耕地观察与实际工具边界

- 已追溯原`6774d22b/#535`观察器、`ecb8fab0`快照锚点及对应JSON记录：旧move／clearance限制是原有意图，不是旧bug。新增默认省略的`action_origin_tilling_resources`开关，只有training_singleplayer、原till及source option、完整六层witness请求才能走显式till scope；原scope身份／预算核、采集器、锚点、XML／graph／debris读取实现复用，默认入口继续拒绝till，不新增执行器或自动启用主循环。初版遗漏锚点门，由实际C拒绝暴露；补同一显式合同及反例，未放宽旧默认门。
- 最终同代码相关 **Backend277/277（14秒）＋Core69/69（不到1秒）**，零失败／跳过／产品构建警告／错误，前面的124／153／33均为其子集，不相加。首次新Backend测试引用不存在的RuntimePrimitives依赖，已把scope测试放回原有引用的Core测试项目，不给生产或Backend新加依赖。全部原失败留存。
- E新隔离校准E由原`ReadAfterExecutionSnapshotCoreAsync`取得`product_after_snapshot_cache_match`；before tick664/hash `28afa30ed2ca42ea2465ea811c6c9450d0738ac0fbbc82eeeec88fe5cc234f8e`，原锚点after tick701/hash `c2af036caea60d600a286dd3b6a93b51021605e061f5e3a1509ac67e81bd85c2`。原观察为observed／零diagnostics，原debris graph／库存数量读取通过、pickup单位0；**原槽位XML核正确拒绝Hoe的hasBeenInInventory false→true、currentParentTileIndex 21→23、swingTicker 781697→781698**。只读局部分析SHA `26936baabd0aea4b12d2aadd3c6776da7e29681959298038b3caf48d438bfe19`，7原输入hash不变，status=partial_observation_only、槽位metadata／完整资源／owner／formal均false。debug setup仅校准，无G19／生产信用，游戏已停止。
- C保留为新锚点接线失败；D观察已observed，但本地校准脚本错误fresh重读得到同tick异hash，不能当原锚点或拿来补造。已改用上面的原产品读取函数，E才是可用固定帧材料；未修改产品端既有正确缓存协议，也未把D晋升PASS。原生HandleTreasure及createObjectDebris共5个精确MethodDef实时反编译零诊断，来源SHA／MVID继续固定；首次旧研究工具的3重载上限拒绝实际4重载，失败保留，后续限定提取成功。
- 原API同会话互审精力绑定叶未见具体缺陷，未独立运行测试或重放原生。首次因遗漏外部任务目录admission没有审到代码，一次离线修正后完成；分别2／11个step_finish、失败和短报告均留存，退出撤权、授权accepted-idle，费用未知。skill专用launcher现于载入凭据／付费前检查外部任务目录双分隔符及三份必读任务的精确相对read规则，6/6离线反例通过，不自动提权／试探调用；其他source read仍须按原安装版规则检查。
- **唯一接续：** 对实际E这三项Hoe XML变化按当前原生方法逐项核因，在新till消费者中绑定，复用原槽位比较核，不能删除旧校验或归一化未知字段。这是原所选till资源接线的工具边界，不是新动作模块。再核同次可能Clay／Treasure／拾取，接同owner claim／rebind／恢复及managed实跑。G19保护、完整生产／销售／保存／新进程重载缺口、L1冻结19/19／11/11、formal=false、PR567草稿、#339/#550/#569开放及local-data不提交不变；#569最新分工没有改变施工顺序。

## 2026-11-13T20:18:00+08:00 R2 耕地精力与真实单次资源重放

- 原API同provider／模型／session完成纯数值Hoe精力函数，`a94a1f33`审查接入`dbfafca4`；保留原生float顺序、efficient跳过setter、祝福仅跳过下降、setter的[-16,max]及高等级增益，不拿旧double候选投影代替实际消耗。27项纯数值测试首次通过；进程退出0、临时权限已撤回，授权accepted-idle。15个step_finish原token字段及短报告已本地留存／校验，费用／余额未知，无fallback。
- 新消费者绑定同frame full源／窄观测的Hoe槽／ID／效率、原库存first-Hoe顺序、effective farming／max_energy及已有statue_blessing；只给原farm_layout有界投影增加`hoe_is_efficient`，不增加默认全量扫描。复用原pet-bowl镜像比较和地形／统计叶，比较视图只归一化核实的精力／碗／HoeDirt／dirtHoed，原件／hash不改，不是新执行器或原生快照。未知库存／工具／debris／持久变化继续拒绝。
- E新隔离校准B真实执行一次原`till_soil`，同tick664的before full与窄观测hash分别为`30d9544d7144bf854d6cbdd52f8a619c5634980699cefa05c5f7c4ca24eca061`／`82a780c13ffc418263b01c5ca80ac3146369c38a9a9f0bbebf2321c1a24eab41`；after tick701/hash `201344e2a9ec1709edd7fee6c1959c59aaa24c11f1f426a95c75c1794aa38b2c`，精力270→268。原持久变化仅farm.pet_bowls／terrain_features、player.energy及GSQ统计，组合原比较核后剩余变化为空。只读重放报告SHA `33d7494e54eb25348a380b9fbff561dd5efbb7623d8d3e91e1b89f916a15f290`；原7输入hash不变。显式debug setup只是校准夹具，**不是G19派发、owner入账或生产信用**，游戏已按归属停止；A缺before窄观测仍只保留局部证据，不补造。
- 最终同Release产物相关 **153/153**（14秒），含数值27、新精力28、地形统计19、准备19及原harvest／watering相关60，重叠不相加；零最终失败／跳过／构建警告／错误。首次新测试JsonNode误用Remove导致编译失败，改为AsObject后93/93再回归；失败原样留存，未修改旧validator迁就。
- **唯一接续：** 这只完成所选“无额外资源变化”分支；当前原观察器明确只接move／clearance，不得偷偷放宽或把随机Clay／Treasure等可能分支当不存在。先沿原有界观察／库存payload／debris比较核，显式绑定所选till额外产出／拾取，再将原编译准备和效果接同owner claim／rebind／恢复；G19派发保护仍在。随后才完整生产／销售／原生保存／新进程加载。#569实验室在正式manifest前接手、L1冻结19/19／11/11、formal=false、PR567草稿、local-data不提交均不变。

## 2026-11-13T19:25:00+08:00 R2 所选耕地编译准备与地形统计叶

- 新`CropTeacherSiteTillCompilation`只读准备组件复用原观测绑定、原整地plan重建／共享frontier、共享动作配方、原ActionQueueCompiler及原queue比较器。分别pin当前full源／ledger、窄观测、原site request／plan／窄model、新full model／queue及原生产plan／request／snapshot／ledger；核原active布局／日期、先完成接近、首个direct till、候选／工具／目标／原短路段参数，不修改原件／source hash。生产plan路径先核一致再读取依赖，Hoe工具槽／ID／精力投影的根与地块镜像须一致。
- G19真实留存输入已独立打开该组件，仍选`till:Farm:71,16`，13份依赖固定且校验不变，full与窄观测hash分别保留，报告SHA `7a4ca3fd4093aadedfb04051ce692246c4a3f65fa0ab43b63bdb6fcd1979a171`。这只是**实际原生输入上的编译准备重放**；native／resource／formal标记均false，无新派发或G19入账。
- 资源层新增限定比较视图叶：只接受原空目标新增唯一的三字段HoeDirt terrain row、当前actor原生`dirtHoed`恰好+1；拒绝其他地形／actor／统计变化、未知target属性与未绑定溢出。复用原resource-envelope语义比较，原harvest helper改为同一实现的薄委托，旧默认规则／消息／执行器不变。仅归一化已验证地形与统计，**精力、工具、库存、debris等仍保留给后续核**，原快照及hash不改，比较视图不能作为真实snapshot或完整resource PASS。原生`Tool.DoFunction`／`checkForBuriedItem`及dirtHoed getter/setter也按当前E SHA／MVID实时提取，Farm无override由metadata确认；本地共6个MethodDef零诊断，不把埋藏物当不存在。
- 最终同产物相关 **105/105**（7秒），零失败／跳过／构建警告／错误，含新准备19、新地形统计19、原harvest相关60及共享配方7，重叠不相加。此前相关79/98中19项均在新准备夹具初始化时被原候选缺`player.energy`／`menus.active_menu`拒绝，补正确夹具后19/19；新命名空间／测试括号失败保留，不改旧validator迁就。新的拒绝断言改为具体InvalidDataException，并补投影镜像矛盾反例；真实earlier-clear组合fixture及专用CAS篡改组合未额外宣称覆盖，原plan精确重建和既有retainer保护继续复用。
- 原API同session Block48达到两次测试上限后退出／撤权：先16个新夹具hash失败，唯一修正后因缺SnapshotHash命名空间未编译，无peer验收通过。四文件草稿保存在peer私有`11bf4381`，控制端以no-commit接入、修夹具／核输入顺序和镜像，再独立完成上述测试及G重放；未丢弃、未冒称peer全绿、无付费第三次／fallback。25个step_finish的原token字段留B，费用未知。当前协作授权保留、accepted-idle，进程不运行。
- **唯一接续：** 完成同一次till的精力／原Hoe字段及可能埋藏物／拾取变化核，再接原owner claim／rebind／恢复；G19派发前保护仍在。先查可用旧原生材料，当前`artifacts/runtime-till-soil-smoke/...`仅空目录，不能当有证据或重开旧已过动作验收。缺实际工具效果时，用**新E隔离校准**采单次原Hoe before／after，明确debug setup只是校准、无生产／Teacher信用；不得用旧TillSmoke默认`saves`或“最新存档”误选玩家／旧档，不拿G未保存尾部当新游戏存档。随后才新managed原生闭环验收。完整生产／销售／保存／新进程重载尚未通过；L1冻结、formal=false、PR567草稿，local-data不提交。

## 2026-11-13 R2 所选耕地的原完整源编译与共享配方

- G19留存原till模型在**另一个内存对象**绑定G18 accepted full源后，原ActionQueueCompiler直接得到单个pending till_soil，无diagnostics／blocker／missing factor。原full／窄观测同tick12033、分别保留hash，五个入口文件未改。报告SHA `7c5d583a68d731251bdaa27fbf2e35a559679df21293c68e48f8179200d2e206`；仅编译诊断，没有派发、资源结算或formal信用。
- 将原通用`BuildCropTeacherActionModel`配方共享到Core，原Loop入口仍委托同一实现；原整地frontier移入Bootstrap共享，Loop公开facade保留。模型目标／actor／参数／source身份及原选择顺序、对象身份、空／阻塞行为不改，不另写till选择器。最终相关27/27通过、零失败／跳过／构建警告／错误。G19**整个原模型**与共享配方DeepEquals，原frontier仍选`till:Farm:71,16`、原件哈希未变，报告SHA `db3097225984256a3d7868baf27ff1c55d4efe2bed123dbe52fbfa15752af7ca`。
- 当前E原版DLL按SHA／MVID实时提取Hoe.DoFunction `0x0600183a`及GameLocation.makeHoeDirt `0x06000fac`，仅两个MethodDef、零反编译诊断，研究输出留本地不提交。确认需核工具／精力、土壤与DirtHoed以及原生buried-item分支；这不是资源效果验证已完成，base Tool与所选Farm派生分支仍按实际调用追溯，不根据文档例子忽略掉落或其他持久变化。
- **接续不变：** 先用共享原frontier／动作配方和原编译器绑定原site request／plan／观测、当前完整源／ledger、候选／工具／目标／日期；并行核所选这一次till的真实资源／土壤效果，再接同owner派发／rebind／恢复。未验证不能派发；不用第二套选择器、执行器或比较核。G18站位／只读恢复已通过不重做，完整生产／销售／保存重载仍未通过；L1冻结、formal=false、PR567草稿、local-data不提交。

## 2026-11-13T18:41:00+08:00 R2 原生站位接近与只读恢复通过

- `64f84c6d` 已推送，代码CI **43408739507 SUCCESS**。新E隔离G `managed-g-20261113-1827` 使用原managed／Product／Teacher入口，后台隐藏静音：4次开店等待、6次购种、3段返回及3次短控制等待后，iteration18的原`move_to_tile`实际到Farm **73,17**，资源中性／回执核及同owner rebind接受。共 **17次owner acceptance／14条verified action**，另3个短控制不计动作／作物／策略credit；site结果`native_site_approach_verified=true`、`route_target_reached=false`、作物／Teacher／formal均false，未将“在Farm”误记站位到达。
- 原accepted after18为day0、tick12033、hash `86d28791babe57f63a32eed32798814d64b4e9b00931a43526e9d2cc0027c02e`。原窄观测／模型留存，新full模型另建，来源hash不同。G在iteration19派发前停止：原整地队列为单个pending **`executor.till_soil`**，无compiler diagnostics，未生成execution-0019。不是站位接近仍失败，也不是耕地／清障已执行。
- 停游戏及Product后，原`resume-crop-managed-owner`只读恢复同一owner成功，返回同一after18/hash，`native_dispatch_performed=false`。未发生原生保存内容读回、新游戏进程重载、销售结算或完整managed PASS；相应标记继续false。随后backend也按原PID／命令行／创建时间核身份停止，四个归属进程均退出，工件未清理。严格核值且拒绝覆盖的本地摘要SHA `1e9e83e17c19bb16d96487494fbee88239b9288e47e667f223c7a0c27da24ddd`，输入／原生原件保留E，不提交local-data。
- **唯一下一步：** 以G19实际原request／plan／till模型／队列及G18 accepted full源为依据，先核原编译器是否可直接为完整源重建该动作，并绑定对应窄规划观测与原候选／工具／目标／日期。再复用原资源／工具／地块比较基础，给**所选这一次耕地**接真实消耗及土壤效果、同owner claim／rebind／恢复；未验证效果不派发，不重写旧执行器、不补造资源／回执或新建第二套比较核。随后才原播种／维护／收获／销售／原生保存／新进程加载。已通过站位接近不重复施工或再列为缺失。
- #569已重新核对，实验室在正式manifest前交接后的职责不变；原API本块实现已接受并退出／撤权、授权accepted-idle，不是停用或仍计费运行。仍R2第3/4步、L1冻结19/19／11/11、formal=false、PR567草稿，#339/#550/#569保持开放。

## 2026-11-13T18:24:00+08:00 R2 原场地接近接同一 owner

- 原API协作者完成限定Bootstrap准备／结果及测试四文件，`469313db`审查后接入`c9a47805`；控制端独立完成Loop入口、原admission／intent／claim／acceptance／恢复接线。显式`OpenSiteApproach`不改变旧默认单connector／控制等待合同；继续使用原站位算法、共享配方、原编译器与原路线资源／回执核，另核实际到达精确站位，`route_target_reached=false`，不把同在Farm当已到站位。
- 原窄观测／模型留存不改；新模型绑定已接受full源，两个hash分别保留，执行结果要求full资源快照。原12分钟／repair4／fresh replan保留，已知清障队列仍须专门资源绑定；不筛掉clear项，不放宽未知持久变化，未绑定的整地动作仍派发前拒绝。站位结果仅登记自身原生证据，不授作物／Teacher／formal credit。
- 最终同产物相关 **114/114**（32秒）通过，零失败／跳过；包含原路线、原生等待、缓存接续、共享配方、新绑定和owner中断／marker／acceptance恢复，重叠不相加。原入口HTTP测试实际编译原窄模型与新full模型，再复用原执行adapter，单次派发／单次rebind。最初新代码多传StandardResult参数、测试误用internal成员、复用恢复目录及不完整envelope／模糊文件匹配的失败均保留，仅修新调用／夹具，不改旧核。此为声明原生结果的测试，不是游戏实跑或完整闭环PASS。
- `8c741045`的CI43404650427失败在旧源码字符串守卫：共享配方提取后它仍要求Loop内含repair参数字面量。已改为同时要求Loop调用共享构建器、不得残留内联参数、共享配方仍repair4；实际行为测试与原配方重放保留，不删除防回退意图。修正提交的CI另验。
- 协作者聚焦24/24（此前22/24为测试站位／可重绑不透明ID的假设错误，唯一修正未改核）；真实进程退出0、临时权限已撤回，本块accepted-idle，授权保留，无替代provider／内部代理。记录23个step_finish的原token字段，费用／余额未知，不从cache token或零cost推断。#569及全部今日更新issue已再核对，无新增施工顺序变更。
- **唯一接续：** 新E隔离档后台静音走当前入口，实证越过原iteration18站位接近断点，并保留首个真实整地／资源断点；再接所选原整地效果与生产／销售／原生保存／新进程加载及只读恢复。当前仍R2第3/4步，L1冻结19/19、11/11、formal=false、PR567草稿、#339/#550/#569开放，local-data不提交；无D盘／正式训练／清理。

## 2026-11-13T17:46:00+08:00 R2 原有界接近配方共享与真实API互审恢复

- `e07307ea` 的CI `43401482428` 已成功。沿本步骤继续提取原`BuildCropTeacherBoundedApproachQueueAsync`的模型配方到Core共享构建器，旧site／refill生产端调用同一实现，后续新消费者可用它独立重建原模型。Farm目标、actor、目标坐标、candidate／source身份、12分钟预算、repair=4、fresh replan、purpose及失败策略均原样保留；不是第二个规划器，也没有关闭顺路开路。
- 当前产品产物聚焦35/35（含原refill编译拒绝及观测回归）、原生产端＋原backend编译入口和recipe最终4/4通过，零失败／跳过／构建警告／错误，重叠不加。F留存的**整个**原site模型与新共享配方JsonNode.DeepEquals一致，原件哈希不变，离线报告SHA `5550aae978220de5405cd19131ec49936a7cb823d3d991d17d657fb05f0cf57a`；并非新编译队列、原生移动或资源结算通过。actor子树克隆不重挂／修改调用者原对象。
- 原API协作者保持原`openai-direct/gpt-6.1-sol high`／同一会话，已真实完成20行独立审查，范围为e073观测／选择组件，未发现实质缺陷；未运行测试、访问E原始存档或证明后续配方／native链。进程退出，临时edit／bash／external权限撤回，授权保留accepted-idle，不冒称仍在运行或永久停用。
- 权限修复来自安装版1.17.11的实际read资源合同：工作树相对路径（含外部任务文件的相对形式）与Windows／slash分隔符；不套用V2配置。已有API失败任务包8文件＋peer AGENTS已复制校验归档，新包6516字节。首次只离线核5个允许／4个拒绝读取、原provider配置不变，再通过专用ValidateOnly；随后一次有用审查成功，无试探调用／重试／fallback／内部代理。skill已写明read同样是相对规则，以及ValidateOnly不验证read可达性的局限。本块记录8个completed step的token字段，账单余额／费用仍未知，不从Codex额度或零cost推断。
- **唯一接续不变：** 用已绑定的窄观测选原站位，借共享配方为**已接受full源**生成新的原机械模型并走原编译器，保留各自hash／路径；不是原位改窄模型hash或合并快照。随后显式限定site-approach角色，核原生到站位与真实资源效果，接原owner claim／rebind／恢复；旧单connector路线与派发保护不放宽。再原整地／播种／维护／收获／销售／保存／重载，不新增未选局部门。#569分工已回复核实，formal=false、L1冻结、PR567草稿、local-data不提交；无新游戏／训练／清理。

## 2026-11-13T17:25:00+08:00 R2 整地补充观测绑定与实验室交接责任

- 主线仍为F原生iteration18的整地接近，不另起动作系统。原accepted full源没有site context；同tick的farm_layout窄观测有上下文却没有完整持久物品图，不能用窄快照替换owner头或补造完整资源证明。新增只读`CropTeacherSiteApproachObservation`将原run request／ledger／full源／窄观测分别pin，核原active布局、同存档／角色／版本／tick／日期／时钟／位置、完整有界地块与地图宽高，复用原站位选择。来源hash分别保留，native／resource／formal标记均false。
- `c196f6c6`的原站位算法提取到Core共享，旧Loop公开入口保留并委托同一实现，仍保持最近安全／非种植位优先／Y-X排序／已有站位不移动及首个有效重复项对象身份。不把窄观测合入full JSON、不改变旧profile默认范围或单connector路线保护，也不丢掉原模型声明的4次有界开路。
- 绑定最终22/22、旧入口及兼容性7/7通过，零失败／跳过／构建警告／错误。实际F四份原件的严格离线重放复现原站位73,17、原件哈希不变，报告SHA `16042baa676153de8fa98d7e6847583e188e9a9e6ca7d575bd5541265c52d065`。只是已有原生观测的绑定重放，不是新移动／结算／托管全流程PASS。
- 重放先发现新消费者误要求map.id等于Farm；原生两份map.id实际均为空，地点身份由player／context给出。只修新消费者，按原map ID一致性及宽高核验，正例夹具加入空ID。首次临时PowerShell未设置遇错即止，错误后写出空值passed报告：原件保留，SHA `506411c01e80a409e8f8cb9b1e80430fabdba6ea21d40f963e84cc10c7405785` 已有invalidated旁证，禁止消费；严格脚本拒绝覆盖、异常即止且核全部非空目标和false标记后，才生成上述有效报告。失败不计通过，未污染游戏／账本／正式语料。
- 已核对新[#569](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/569)：主线完成R2所选路线原生验收、覆盖／对账及正式准入工程接线，交付版本／构建、可重放语料来源与原Provider／数据构建／训练入口说明；**正式manifest之前交接**。前瞻实验室在当前电脑D盘负责语料冻结、模型定型／实验、正式训练与训后评估，旧电脑承担实际Student＋游戏8GiB整机验证。原准入与Provider要求不降低，工程缺口由实验室提交复现返修；本次不触D、不启动训练，不改变当前整地施工顺序。
- **唯一接续：** 原场地接近model／队列应绑定已接受完整源，在保留窄观测来源和原开路声明的前提下复用原编译器；再把实际原生移动／目标站位、资源中性或实际修路效果接同一owner的claim／rebind／恢复。当前派发保护仍在，不把当前观测组件算这条执行链已完；随后原整地／播种／维护／收获／销售／保存／新进程重载与读回，其他R2门保持。L1冻结19/19、11/11、formal=false，PR567草稿、#339/#550开放，local-data不提交。新额度已刷新，最新用户说明有重置机会时60%限定不生效；不继承旧80%停止状态，亦不擅自使用重置。

## 2026-11-12T20:16:00+08:00 R2 缓存接续实跑通过与整地接近首断点

- `19773b15` 已推送，远端CI `43214258647` 已成功。新隔离 `managed-f-20261112-2003` 走原托管／Product入口，iteration13、15、17三个地图切换后的原等待均被同一owner接受，未再出现旧accepted hash的404；不增缓存／重采全量快照或修改原资源／时间核。共16个owner acceptance，其中13条计入verified action，3个短控制等待仍不计作物／策略credit。
- 原链正常完成6次购种和3段返回路线，最后原after-snapshot-0017确认为Farm、day0、tick11969、hash `9764670a8e728239ba397c8a11cc2ae02807a09fca6ec7a755ac0c9845e0126d`。这次“已回农场”有原快照依据，不沿用E仅到Town的错误口头说法。
- iteration18首断点在原`PrepareSite`保护：已有`crop-teacher-site-approach-0018-compiled-queue.json`只有一个pending的`executor.move_to_tile`、无blocker，但尚未绑定托管资源／owner消费者，故**派发前拒绝**。不是原移动执行器缺失，也不是此步已执行耕地或清障；不因queue pending而直接放行、不预先修改存档／地块。
- 游戏／Product停止后，原`resume-crop-managed-owner`在原backend上只读恢复同一owner成功，读回相同accepted hash；报告`native_dispatch_performed=false`、保存内容／fresh重载／complete／formal标记均false。此为当前实际accepted链恢复，**不是新进程游戏重载、销售保存或完整A通过**。最后backend也按记录PID／命令行停止，4个归属进程均已退出，留档未清理。
- **唯一下一步：** 按R2第3/4步，先把原场地接近`move_to_tile`的原model／编译／native回执／目标站位及持久资源不变验证接入同一owner与原rebind／恢复；复用原移动执行器和比较核，不能把它生搬为只收单connector的原跨地图route，也不能把已在Farm误当目标站位已到。随后对所选原整地／耕地或清障的实际资源效果接线，再继续播种／维护／收获／销售／原生保存／新进程重载与完整只读恢复。未选全剧情／全清障不加局部门。
- 聚焦30/30、相关156/157及原File.Move失败独立1/1、最终独立拒绝反例＋原失败项6/6均按前节范围登记，重叠不相加。L1冻结19/19、11/11、formal=false，PR567草稿，#339/#550开放，local-data不提交；原API本块权限失败退出后未再调用。周额度最新79%USED，接近约定80%USED，安全收尾不启动下一轮长测；目标仍未完成，后续从本条继续。

## 2026-11-12T20:00:00+08:00 R2 真实购种接续与有界快照缓存

- `1d34b008` 的 CI `43208213320` 已成功。新 E 隔离运行 `managed-e-20261112-1928` 实际接受4次原等待、6次花椰菜购种和1段返回路线，共11条 verified action。首次农场教程资源断点已越过；**返回路线仅到 Town，不是已经回到 Farm**，先前口头“返回农场完成”的表述撤回。尚无播种、跨日、销售／保存重载或完整A通过。
- iteration13 原生控制等待已留存结果，随后按原 accepted hash `ae6a5ddc…c9b6100` 读账本404。原 accepted 快照tick10729，主循环新观测tick10770，等待后tick10801。原后端从`90b21bb4`起只保留两个快照；这是内存保护，不应扩容或改成读取latest。新消费者未在结算前重新导入原 accepted 源，后续观测已将它淘汰。
- 新托管 supporting 主入口在原生结果通过原核后，按已pin的原输入重新导入 source／after，再运行原账本rebind／读回。复用已有恢复入口的精确导入函数，抽出同一helper供两处调用；要求accepted=true及返回hash完全一致，读取压缩留档而非重采游戏。原StateStore上限、owner、执行器、资源／时间／原生核和未知执行不重发规则不改，没有第二套账本或执行器。
- 聚焦30/30通过；同产品产物相关157项中156通过、1项旧夹具初始化File.Move访问拒绝，独立复验1/1通过，失败保留，不能把首次相关运行写成全绿。最初新测试namespace、gzip夹具释放和helper导入编译失败均保留；新测试仅释放进程内host并留存证据，不为夹具修改旧保护或添加清理。最终加强反例将accepted=false／错误hash分别单独触发，连同原失败项6/6通过，零失败／跳过／构建警告／错误，重叠不相加。
- 原API同一会话只读审查两次因应用路径权限失败退出，无报告／代码验收，本块不再付费调用；不是额度耗尽结论。授权与空闲／受阻状态分开，其他provider／内部代理未启用。
- **唯一接续：** 提交此接线修复，再用新E隔离档走原入口，验证越过旧hash淘汰断点并继续所选合法生产／销售／原生保存／新进程重载／同owner读回和只读恢复。E已执行等待不重发、不补造缺少的原账本mutation journal。仍为R2第3步收尾／第4步实际验收；L1冻结19/19、11/11，formal=false、PR567草稿、#339/#550开放、local-data不提交。周额度79%USED，接近80%USED安全收尾，不把未完目标登记完成。

## 2026-11-12T19:20:00+08:00 R2 首次农场教程的精确资源分支

- 前片 `f7364d7c` 的 CI `43203117611` 已成功。沿 D 首断点和当前 E 两个 MethodDef，购种分步消费者新增限定原生教程分支：必须为 applied／fresh／verified 的原 traverse_connector，源／终点 hash和tick一致、FarmHouse→Farm、原版1.6.15。当前玩家只新增button_tut_2，且GSQ当前玩家行同步；不把此UI标记授作物／策略credit。
- 分支复用原 PersistentStateRows／HashCanonical，对**比较视图**移除这一个已验证增量后比较全部边界；原快照／hash／原始邮件不改，默认TryProject仍判定这两个字段有变化。其他玩家、其他邮件、任务／世界未知字段、库存／资金／身份／日期仍保护；状态不可读、缺镜像／重复玩家／非当前行、额外增量或其他动作均拒绝。没有新执行器／账本服务，亦未全局忽略邮件或改原通用资源投影。
- 最终聚焦 **50/50**、同产物原购种／归属／幂等／拒绝回归 **49/49**，零失败／跳过／构建警告／错误，重叠不相加；新26项包含输入字节语义不变、默认严格投影未被改变及上下文／镜像／未知资源反例。原D未改件重放调用原BuildSupportSlice成功，stage=purchase_slice、单价80；5个入口文件哈希前后不变，输出SHA `91efe08cc48a9419879f173827103aad95342e0bc5f08ac0b90b6ceea141fa1b`。这是**旧原生证据的离线重判**，不代表D被补写结算、新游戏PASS或整轮完成。
- **唯一接续：** 交叉审查此边界、用新 E 隔离档接现有主入口继续合法生产／销售／原生保存／新进程重载／同owner读回和只读恢复。D已执行购种不重发、不修补旧失败工件；原保护意图追溯cb7bbb071，仍拒绝未知进度。未通过播种／跨日／完整A，PR567草稿，L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交。

## 2026-11-12T18:45:00+08:00 R2 已入账控制结果与首次农场教程断点

- 新隔离 `managed-d-20261112-1826` 使用 `bcf8af9b` 的产品源码，租约／原候选接续／full资源输入已实际越过前两断点：同一 owner 连续接受4次原生等待及资源结算。iteration6随后10个原生步骤均 applied/verified，正常出屋→农场→城镇→皮埃尔柜台／开店／等待／购买／关菜单，末态9:10、金币500→420、花椰菜种子0→1（开局防风草15不变）；但该购种波次**尚未被账本接受**，不能把原生成功当已结算收入或完整闭环。
- 首个新的资源断点在 step1 的 FarmHouse→Farm：仅新增 `quests.mail_received` 的 `button_tut_2` 及 `world_progress.game_state_query_unlock_state` 镜像，原纯支持步骤核拒绝。现已对当前 E 游戏 SHA `7f1e5b8e…26deaee`／MVID `aa1f513a-94f0-4ef6-a35f-2d5b4a3721bf` 实时提取两个准确 MethodDef，零诊断、无原生执行：Farm.resetLocalState（token0x06000e35、IL SHA `f5b523d0…933b19`）在首次进入时写此标记并创建 ButtonTutorialMenu；TitleMenu.createdNewCharacter(true) 并不预填它。不是测试新档漏模拟初始化，不得向存档预塞标记或全局忽略邮件／世界进度。
- `616a186b` 远端 CI `43197619479` 为2507通过／1个旧源码位置断言失败；守卫现按新前置归属核与原租约内校验／执行／结算／接受分开核，并补托管购种接续及full资源输入断言。最终定向 **24/24**、零失败／跳过／构建警告／错误；首次测试构建误触运行中产品DLL发生文件锁，失败原样保留，改为仅构建测试项目后通过，未为此重启运行或改生产守卫。D的产品源码未因测试修正改变，游戏／服务现已按记录归属停止，无清理。
- **唯一接续：** 按实时原版依据给这个已观察的原生教程副作用接精确控制结果／资源消费者，复用原规范投影，保护所有其他邮件、其他玩家镜像、金币／库存／任务／未知字段；先重放D首断点与反例，不能重发该已执行购种请求。随后新隔离档继续所选合法生产／销售／原生保存／重载／同owner读回和只读恢复。尚无播种、跨日／保存重载或完整A通过；L1冻结19/19、11/11，formal=false，PR567草稿，#339/#550开放，local-data不提交。

## 2026-11-12T18:23:00+08:00 R2 托管购种接续回执的真实消费断点

- `616a186b` 已推送。新档 `managed-b-20261112-1808` 越过租约断点，但本次测试命令误将 product 请求指向 harness 的8767端口而404；属于控制端测试接线错误，未修改产品规则。后续 `managed-c-20261112-1813` 复用既有 ProductExecutor 独立8768服务，健康报告绑定本 run、原8767及原桥，无新执行器。
- C 已取得真实原生等待回执：1800ticks、6:00→6:40，primitive verified；product 终态与事后 full 请求快照同 tick2464、同 hash `699773f68eb115e7bce4470c46ec6879a789d0422149cbe59b1446ae1bf8ffe9`。原收尾核仍正确拒绝：新主入口沿用作物机械批次的关闭日计划接续参数，原执行循环未生成 selected_queue_decision_complete／resume_index；同时购种沿用的 shop 窄快照不能替代原完整持久资源核。等待不是购种成功，未入账／未取得完整 managed PASS。
- 仅调整新消费者：托管购种向已有 ExecuteRuntimeTestHarnessCoreAsync 启用其原候选接续租约，外层仍由唯一 owner 管理；托管 seed 结果使用 full 请求快照，旧普通路径窄化不变。不补造回执字段、不改原执行器／接续验证器／时间／资源规则，不重发已声明或未知执行。最终相关 **184/184**（52秒），零失败／跳过／构建警告／错误，包含新托管 full profile／URL断言与原购种、移种／播种租约／前缀回归；仍不是新入口接续的原生通过。B/C首断点及产品回执保留，记录归属的游戏／服务已停止，无清理。
- **唯一接续：** 验证并提交本次调用参数修复，再用新 E 隔离档复验真实购种／生产到销售、原生保存、新进程加载及同 owner 读回／证书／只读恢复。仍 R2 第3步收尾／第4步，A完整与无清障覆盖未证明，L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交。

## 2026-11-12T18:05:00+08:00 R2 首次托管原生运行的租约顺序断点

- 前片 `aa6c4b37` 的 CI `43193886486` 已成功。E 隔离新档 `managed-a-20261112-1750` 只加载原桥／harness，后台隐藏静音，正常领取15颗开局种子；显式当前 managed/product/receipt/save 入口由原排名交 Teacher，生成84动作计划并安装同一 owner。**首断点在 iteration2、acquire_seed：主入口在 owner 执行租约前调用原队列校验，原守卫正确拒绝。** 无 dispatch claim、原生动作、收获／跨日／保存读回或完整 managed PASS；不是缺深层掉落字段。
- 追溯 `8e540c72` 的旧提前校验与 `e333a279` 的原租约合同，只适配新入口：非托管仍提前调用原校验；托管先核原 owner／pending／prepared runtime 归属，在已有 ExecuteSeedAsync 租约内调用同一个队列校验／完成核。旧 owner、执行器、资源／时间核不改，未知执行仍不能重发。
- 聚焦最终 **8/8**、同一构建相关 **184/184**（54秒），零失败／跳过／构建警告／错误，重叠不相加；原生失败及新测试夹具缺候选身份、回执副本未同步和测试 namespace 编译失败均保留，不修改旧共享夹具／校验器迁就测试。原运行已退出，仅停止记录归属的游戏／backend，工件未删。
- **唯一接续：** 提交此断点修复，用新 E 隔离档重新接当前托管入口，继续同一 owner 的所选合法路线→真实销售／原生保存→新进程重载／读回证书→只读恢复。A 的完整通过及无清障覆盖尚未证明，不拿旧 smoke 或声明测试替代；仍 R2 第3步收尾／第4步验收，L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交。

## 2026-11-12T17:41:00+08:00 R2 原终局证书与保存读回绑定

- 前片 `b642a79f` 的 CI `43187885475` 已成功，仍沿 R2 第3步、PR #567 草稿。原结算组装器现由同一核心支持只读重放：验证现存五文件，不创建／修补／覆盖；原 BuildAsync 默认拒绝覆盖、原收益／序列／native／资源规则不变。读端只接受实际 trace 或 trace 已 pin 的原生产 manifest 输入来源，后续 load 工件不倒灌到旧证书。
- 自查发现原生产 manifest 的引用仅留在主循环内存，未进入可恢复的终局 intent。现由原选择器将其路径／哈希接入原 admission／intent／acceptance／trace，接受回执后保留引用，接续不能丢弃或替换；首次绑定要求原生产 head／ledger／计划／留存路径一致。旧无引用校准调用保持原合同，不能借此得到新完整收尾标签；不重复构建整波、不新增执行器。
- 保存收尾端取同一 accepted 链最后原生 overnight 的原 snapshot／ledger／保存指纹和已接受销售目标，调用原选择器／只读结算重放，再绑定当前已验证读回及完整输入哈希。主入口只有本次读回和原终局均核实才标 complete_managed_runtime；不把 reset tick 新头当旧原生终点、入箱 pending 当收入，formal／Teacher 标签仍false。完整 owner 正向串联尚待实机，不能从当前分项测试晋升实际通过。
- 原 API `9e0f90e3→11afa744` 三文件保留；首轮缺新文件 namespace 的编译失败经唯一修正后16/16（原8＋新8）。控制端最终聚焦 **34/34**、同构建相关 **260/260**（2分42秒），零失败／跳过／构建警告／错误，重叠不加；含只读字节不变、缺件／篡改／warm cache、引用跨重开及篡改／丢弃拒绝、缺保存／销售不能晋升。声明／HTTP／原 golden 重放不是新的完整 owner 实机 PASS。协作者退出撤权、授权仍空闲，成本未知，无 provider／内部代理替换。
- **唯一接续：实际运行验收。** 在 E 隔离存档接当前 opt-in 托管入口及上述恢复命令，走所选合法无清障 A 的购种或声明已有种子→播种→维护／收获→销售／原生保存→新进程重载→同 owner 读回／证书绑定，并验证一次原只读恢复。现有 `Invoke-RuntimeCropTeacherFirstDaySmoke.ps1` 是非托管旧入口，需显式接当前模式，不能直接计新 R2。保留第一个实际断点再修，不重跑冻结 L1，不把未选剧情／清障追加为前置；L1保持19/19、11/11，formal=false，#339/#550开放。E测试临时目录CPU1／heap1GiB，无游戏／服务器／训练／部署／清理或 local-data 提交。

## 2026-11-12T16:56:00+08:00 R2 原 owner 显式恢复与重载主入口

- 前片 `6f829689` 的 CI `43181617520` 已成功，仍为 R2 第3步、PR #567 草稿。新增 `resume-crop-managed-owner` 命令，消费原 header／初始输入／索引／lease，先走原恢复，再接已有实际进程／桥／日志／保存指纹采集、原 full snapshot ingest 和同一 owner 单次读回。普通旧入口不暗用这些参数；没有新执行器、游戏启动／关闭或 native 派发，旧资源／24tick／legacy fork 拒绝不变。
- 仅指定 owner 时不创建未开始的账本提交；显式给出与 pending 相同的观察，才可完成其原读回 marker。已接受的同一观察再次请求只核旧结果，不重复登记或把历史恢复算新重载。原 backend 归一化后按明确 stateHash 导入／读取账本，不把较大旧 tick 的 latest 当 fresh head。输出在 owner 内独立新一级目录，不覆盖原输入。
- 原 API 三文件 `9ac8ebce→f1c7ba05` 保存；两次原聚焦均5通过／3在旧 receipt 配置门失败，达到修正上限退出撤权，未继续付费纠错。控制端只补新测试有效参数、放行原桥已有 profile/fresh 查询；测试夹具新增默认关闭的 product 模式，避免创建旧 harness 版本后才切配置。只读属性赋值编译失败、模式错位与旧夹具收尾不认识新目录的失败均留存，未放宽原生产守卫；product 夹具保留工件，仅释放进程内 host。
- 最终聚焦 **18/18**、同一构建产物相关 **244/244**（2分14秒），零失败／跳过／构建警告／错误，重叠不加；真实 CLI 缺 owner 调用按预期 exit2，未进入普通训练。前次241只属于只读边界修正前产物，最终244才覆盖新增拒绝／显式接续及重复观察反例。均声明／进程内 HTTP及错误参数 CLI，尚无实际游戏／新重载／完整 managed PASS。E临时目录、CPU1／heap1GiB，无清理、部署或 local-data 提交。
- **唯一接续：** 将此保存读回报告与原已留存生产／真实结算证书及 owner accepted 链绑定，使用保存时原生终局 snapshot／ledger，不拿 reset tick 后的新头重算旧证明；恢复报告当前故意保持 `complete_managed_runtime_verified=false`。然后实际同一 owner 主循环＋此恢复入口正向／中断验收及 R2 第4步有界合法无清障 A，保留首个真实断点。不补全未选剧情为前置，L1冻结19/19、11/11，formal=false，#339/#550继续开放。

## 2026-11-12T16:16:00+08:00 R2 同一 owner 保存读回与原运行重开组件

- 仍沿 11月11日更新的 R2 第3步，前片 `049bdc85` 的 CI `43177385955` 已成功，PR #567 保持草稿。本片把已核的读回 scope 注册为独立 `saved_content_readback`：复用原 admission、单次 supporting rebind marker、索引、完整账本读回和只读恢复，不伪造 queue、native claim／result，也不进入移动执行器。run、当前 accepted source／ledger、隔离槽及原保存报告必须绑定本 owner 已接受的 overnight；复制同内容的外来报告或账本也拒绝。保存源与当前 volatile 头仍分别核验，旧资源／执行器／24tick 核未放宽。
- 原 API 同会话两文件 `6d729342` 保存并集成 `2b5d69c4`，聚焦8/8；新 retained-owner helper 只读取原 header／初始输入并调用原 OpenAsync／索引／独占 lease，不重新安装或复制账本，要求当前 run／executor／backend／journal／slot 一致并跨 open 核 header 未变。首次调用被我漏更新的私有 AGENTS 旧 Block36／34 范围正确拦住，无代码／测试；两处说明已改为引用当前 lifecycle／任务包，同会话唯一修正成功，原文件保存并核哈希。进程退出撤权、accepted-idle、授权仍保留，未换 provider／内部代理；美元成本未知。
- 控制端最终聚焦 **17/17**，同一构建产物相关 **131/131**、1分29秒，零失败／跳过／构建警告／错误，重叠不加。含原睡眠后读回、admission 重开、marker／acceptance 丢确认、未知 rebind claim 不重复提交、外来 run／ledger／保存报告拒绝及拒入 native 派发／伪造作物进度。原生产 trace 保留读回输入证据但不把 load 编造为原生产动作；重载后的新头不用于冒充原终局 native 头。均为声明／进程内 HTTP，**未启动实际游戏／重载／训练**。E 临时目录、CPU1／heap1GiB，无未变大矩阵重跑或清理。
- **唯一接续：** 在主入口显式打开此原 owner，接已有实际进程／桥／日志／保存指纹采集和原 snapshot ingest，再走本片单次读回与恢复；保留原终局证书，不在重载后重算整波来适配 reset tick，不放行旧 legacy resume fork。需核 backend 当前显式 stateHash，不能把旧较大 GameTick 的 latest 当 fresh head。随后实际同一主循环正向／恢复及 R2 第4步有界合法无清障 A。当前 helper／capture 尚未接主入口，主循环仍停在 semantic-save/fresh-head；L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交，无游戏／服务器／部署。

## 2026-11-12T15:33:00+08:00 R2 保存内容与实际重载来源消费者

- 前片 `87935184` 的 CI `43171332787` 已成功，仍沿 R2 第3步、PR #567 草稿。新内容叶复用原完整持久资源 projector／未知字段保护和原 supporting rebind，显式核库存／作物／原币种／年月季日可读、原存档／玩家／游戏身份及账本 before source。独立内容相同只能标 SnapshotResourceContentMatches，不能标实际重载／保存语义或训练成功。
- 原 API 三文件 `61958bdd` 已保存并集成 `a68572d0`，10/10聚焦通过；原9通过／1季节反例改成同值的夹具失败经唯一修正后通过，旧核不动。控制端将新叶的同一比较逻辑提取为可复用入口，分别核**保存时的快照**与**当前 accepted head**，实际 rebind 只对当前账本，避免将多次重载的 volatile 头当保存源，未复制另一套资源算法。
- 新来源消费者／只读采集入口绑定：保存后的原报告与指纹、保存文件在读取前后未变、保存后新启动的精确 SMAPI进程／可执行文件哈希、原 harness 的完整保存根／槽加载日志、原 bridge v2 从 genesis 起的完整哈希事件链及 SaveLoaded 后实际发布的 full 快照。日志是不可变有界副本，不把可变直播日志作为未来输入；复制旧快照、错槽同前缀、旧进程、断链／篡改、缺加载／发布或资源漂移均拒绝。不新增桥字段，不放宽旧24tick／原生动作时钟守卫，也没有在此入口启动／关闭游戏。
- 控制端最终聚焦 **24/24**、同产物相关 **60/60**（52秒），零失败／跳过／构建警告／错误，重叠不加；包含真实当前测试进程不等于绑定游戏时，在任何 HTTP 请求／输出前拒绝采集。原两个测试 using 编译失败、一次把纯文本日志误交 JSON 专用 reader 的集成失败均保留，修正只在新测试／新消费者。测试采用 E 隔离临时目录，没有重跑未变化的1059矩阵；全为声明内容／合成进程日志事件／相关回归，**不是新的实际游戏重载或完整 managed PASS**。实际进程采集函数已实现但主入口尚未调用，不能据本片晋升全局／训练；目录318/318无漏项／重复。
- **唯一接续：** 把已核的读回 scope 接同一 owner 的 admission／单次原 rebind marker／索引与只读恢复，再接显式 retained-owner 主入口和实际隔离游戏重载采集；原 legacy resume fork 拒绝保持，不用匹配快照冒充真实加载，不重派发已知或未知原生结果。随后实际同一主循环正向／恢复及 R2 第4步有界合法无清障 A。当前主循环仍明确停在 semantic-save/fresh-head 收尾；L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交，无游戏／服务器／训练／部署／清理。

## 2026-11-12T14:47:00+08:00 R2 原回家睡眠与终局收入证明接线

- 前片 `ca6cc678` 的 CI `43167187296` 已成功，仍为 R2 第3步、PR #567 草稿。本片所选入箱完成后复用原 owned route／控制等待／overnight 和保存指纹，不改原执行器、原生长／日界或旧回家种类。主循环在次日从同一 owner 的已接受历史组装原 recurrence／terminal 证明；只有原验证器核实实际日结才标 terminal settlement，仍明确停在保存内容语义／fresh head 收尾，不冒充整个托管运行完成。
- 新组装层核原生产 manifest 实际哈希、所选原留存证书／目标和完整终局后缀。接近／每件入箱、原 `supporting_route_to_sleep` 的回家／等待和唯一原生睡眠逐项消费，不能过滤中断、漏步骤或把入箱待结算当收入；末态账本必须绑定同一原生快照／存档／玩家。调用原 recurrence／terminal builder，不重算收益、不逐动作重建整波生命周期。输出独立新目录且不可覆盖，semantic_saved_content=false、formal=false。
- 原 API 三文件 `d3b4254a` 已保存并集成 `a2f1f379`，原8项4通过／4在声明 helper 的不同 run 身份处失败，按一次修正上限退出。控制端对准旧合同修正**新消费者**中的泛称 route kind，并在新声明 golden 构建期统一 run／真实末态 ledger；旧执行器／验证器不改。初始失败保留，不把提前拒绝的反例计作目标边界已验证；协作者已退出撤权、授权仍空闲，未换 provider／内部代理，美元成本未知。
- 最终聚焦 **16/16**、相关源码守卫 **42/42**，零失败／跳过／构建警告／错误。含原完整生产／日结声明 golden、无原生睡眠的 pending-only 拒绝、缺入箱／跨头／异类转移／价格／哈希、输出不覆盖与输入不变，以及主循环回家／睡眠选择与未接受游标拒绝。同一产品产物扩大相关回归 **1059/1059**、13分30秒、零失败／跳过，计数重叠不相加。没有新游戏／服务器／训练／MOD部署；本次测试临时目录 I，旧工件未清理。
- **测试落盘诊断。** 当前回归持续有 CPU／新工件进展，未重启或重复开测。只读磁盘映射确认 C／E 同属 Samsung NVMe、I 属 ST1000LM035 SATA；本片把临时目录从 C 改 I 后大矩阵显著变慢。后续临时测试目录设为隔离的 `E:\StardewAITraining\test-temp\<block>`（进程级 TEMP／TMP），项目与归档仍 I；不迁移／删除当前工件，不为窄改重复未变化的大矩阵。E当时10.16GiB、C6.63GiB可用。
- **唯一接续：** 复用既有隔离保存／恢复合同，补库存、作物、金币、日期的保存内容读回及 fresh bridge／accepted head 连续性和主入口恢复；指纹不等于内容，重载的 tick 重置不应变成旧被动连续性守卫的放宽。随后 R2 第4步一次有界合法无清障 A 原生验收，首个实际断点留证处理；不重跑冻结 L1、也不以全未选剧情／清障作局部前置。L1保持19/19、11/11，#339/#550开放，local-data不提交，无清理。

## 2026-11-12T14:17:13+08:00 R2 原终局接同一 owner 派发与恢复

- 仍沿 R2 第3步，前片 `dc2aabb9` 的 CI `43162500304` 已成功，PR #567 保持草稿。本片将既有终局接近／单件入箱接入同一 owner 的 admission、原执行入口、native claim／结果、单次 marker、索引和只读恢复。默认旧模式、原执行器及资源核不变；接近不增加入箱游标，游标只由已接受回执推进，待结算金额仍不算收入。
- 主循环保留一次原整波生产证明与终局选择，不在每次入箱重建生命周期；选定目标／原留存证书／槽位／品质／价格／箱／站位绑定后，复用唯一原模型配方和 ActionQueueCompiler。终局队列独立标记，不误记作物批次完成，也不走通用无主回退。入箱完成后明确停在原生结算／保存收尾，尚不宣称完整运行结束。
- 原 API 五文件 `84327264` 已保存并集成 `4452d78e`，原8项3通过／5在库存源门失败，按一次修正上限退出交接。控制端仅补新夹具缺少的原 `can_be_shipped`、空物品 null 回显和原移动／入箱 ChangedFacts；未放宽旧生产校验。原失败记录保留，协作者撤权／授权空闲，美元成本未知，未换 provider／内部代理。
- 最终聚焦 **34/34**、零失败／跳过／构建警告／错误，含同一 owner 接近→入箱、最后一件原空槽／图删除、原 HTTP 执行入口无通用回退、五种丢确认／重开、未知结果不重派发及游标防伪。扩大相关回归复用同一构建产物 **1043/1043**、4分39秒、零失败／跳过，重叠不相加；均为声明／HTTP 接线证据，未启动新游戏／服务器／训练，不将旧884矩阵算本片。目录318/318无漏项／重复。
- **唯一接续：** 所选入箱后复用原回家／控制等待／原生睡眠，消费原 recurrence／terminal 的真实结算，再接保存内容语义读回与 fresh bridge／accepted head 连续性；随后实际同一主循环恢复及 R2 第4步有界合法无清障 A。指纹不代替库存／作物／金币读回，不扩为全未选剧情／清障局部门。L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交，无清理。

## 2026-11-12T13:31:06+08:00 R2 原入箱资源与终局选择接线

- 前片 `29b8dc99` 的 CI `43158312225` 已成功，PR #567 仍草稿。本片沿 R2 第3步，新增原单件入箱资源消费者：复用原 queue／receipt／native step／deposit 验证器及原库存图、容量、货币和持久资源核，精确保护唯一槽位减1、最后一件的空槽／图聚合删除，以及同一 bank 的多个物理箱镜像、原 contents signature 和其他内容／未知属性。旧只加库存的数量 helper、原执行器和共享规则不改；待结算金额不授收入或资源 credit。
- 原 API 同会话五文件 `45d65712` 已审查保存并集成 `5cebff25`。原首次 fixture setter 编译失败后一次修正，末次8项7通过／1在不存在的 seed_inventory 上修改即失败；按退出条件停止，未继续付费纠错。控制端改成真实缺失→新增漂移反例，补入箱 kind 检查；新消费者复用已有社区中心库存候选／真实进度分离核，支持卖出后预览数量减少或消失，真实献祭／奖励／未知进度变化仍拒绝。未新增桥字段或修改旧核以迎合用例。
- 同一 owner 终局现在固定原生产 manifest 的实际哈希，选择其 accepted head 对应的原留存波次和原脚本要求的精确可出货栈／空完成箱／站位，生成原 authoritative_crop_terminal_continuation 单动作模型并调用原 ActionQueueCompiler。每次只是一件，先走站位、到位后才生成入箱；品质、价格、数量、箱归属和候选身份固定，游标只能来自后续已接受 owner 回执。本片主入口仍仅保留模型／编译队列／选择记录，native_dispatch=false、execution_authorized=false，明确停在 owner 销售／保存接线，不经通用回退派发。整体轨迹只在终局选择核一次，每动作只核精简终局证书，不重放整条生命周期。
- 最终聚焦 **38/38**、同一构建产物扩大相关回归 **884/884**，零失败／跳过／构建警告／错误，重叠不相加。控制端初轮5个同因失败是声明 golden 缺旧站位字段，现复用原日容量夹具 shipping_bins；同时补已有原始 can_be_shipped 投影，未改生产校验。均为声明／HTTP／接线证据，无新游戏／训练。私有 CPU1／heap1GiB 小型读端另重放旧 E 轨迹首个实际入箱：`(O)190`、单价175、待结算0→175，原资源叶通过，四输入文件哈希未变；这不是新的 managed、整波销售或保存内容验收，报告 `block33-native-shipping-replay-a.json` 私有留存。
- **唯一下一步：** 把已经选定／编译的原终局接近与单件入箱接入同一 owner 的 admission／native claim／结果／marker／恢复，以实际已接受入箱回执推进游标；随后复用原回家／观察／原生睡眠与 recurrence 实际结算，补保存内容读回和 fresh bridge／accepted head 连续性，再同一主循环正向／恢复与 R2 第4步有界合法无清障 A 原生验收。不重建生产证明来重试入箱，不将不明结果重派发；指纹不冒充语义读回，其他所选分支仍在原层。L1冻结19/19、11/11，formal=false，#339/#550开放，local-data不提交，无清理／新游戏／服务器／训练。原 API 已退出撤权、授权空闲，未换 provider／内部代理。

## 2026-11-12T12:51:14+08:00 R2 终局复用原整波生产证明

- 仍是 R2 第3步、PR #567 草稿。前片 `8d9baaa3` 的 CI `43154315788` 已成功，不能借其绿灯晋升本片。新 opt-in 主循环到原 Complete 时，从同一 owner 已接受历史调用原布局／日容量／完整生命周期／每波留存 Build，随后仍明确停在原所选销售和保存内容读回待接线处；日历完成、单次收获和入箱待结算均不算真实收入。
- 历史只存序号／路径／哈希，不常驻每阶段 scope／快照图。终局锁内核原 acceptance 链、admission／结果／proof／队列／回执／前后快照；pending、不明 native claim、未恢复头禁止导出。按实际格式消费旧购种标量 proof 和播种／转移 artifact map。已验 owned planting 仅适配为生命周期原 crop_actions，队列／回执不改；取得种子／准备前缀在种植分母之前，分母内部不漏控制或冒充中性。完整原波次／最低产量不降级，不把终局全局证明作为每次动作的门。
- 输出须为 owner 内独立新目录，隔离原输入／索引名／原生 slot，部分结果保留不覆盖。整体边界核验替代每写请求重复全轨迹重哈希；不增加游戏快照。最终聚焦 **14/14**，零失败／跳过／构建警告／错误：原 owner 跨日导出／重开、两种种子 proof、历史篡改／不明结果／输出范围，以及原8日草莓状态机声明轨迹整波重建／缺失反例。声明 golden trace 不是实际 managed owner 或新游戏证明；初次测试误用 internal 哈希工具的编译失败保留，仅改标准 SHA256。同一产品构建产物扩大相关回归 **842/842**，零失败／跳过，4分27秒；随后只优化测试夹具为每进程共用一份有界 golden、缺整日／独立副本篡改反例，末次14/14再次通过，未重复842矩阵，计数重叠不相加。原工件保留，无清理。
- 原 API 同会话只读接口映射22行后已退出／撤权／accepted-idle，无代码／测试。已交叉核实原 shipping deposit、recurrence／terminal、日边界和保存指纹入口；出货只是接口建议，实际计划／波次／终点仍须派发前绑定。**保存哈希变化不是库存／作物／金币的存档内容读回。**
- **唯一下一步：** 整波留存后所选原终局路线／销售（出货需原生睡眠结算）接同一 owner claim／结果／账本／只读恢复，接真实保存内容读回与 fresh bridge／accepted head 连续性，再实际同一主循环正向／恢复与 R2 第4步有界无清障 A 原生验收。不增加全未选剧情／清障局部前置，其他分支责任留原层。L1冻结19/19、11/11，formal=false，#339/#550开放，无新游戏／服务器／训练／清理，local-data不提交。

## 2026-11-12T12:15:00+08:00 R2 安全对话控制结果接线

- 前片 `cd347961` 的 CI `43150917338` 已全通过，仍保留 PR #567 草稿，不能把 CI 当完整托管原生验收。当前仍属 R2 第3步：复用原安全无人物对话判定／原关闭对话模型与编译器，把原回执、资源不变和账本恢复接入同一 owner。
- 原 predicate 提取到 Core 供原 Live wrapper 与验证消费者共用。`dialogue_transitioning` 仍只要求有布尔值，`available` 与 Sleep／SleepTent 排除等旧语义保持；不复制判定或收紧旧动作。新增 Control 结果分类与准备／验收记录，不是第二个执行器；沿原 supporting 派发、claim、marker、index 和只读恢复，不能冒充回家、浇水、收获或睡眠成功。
- 新消费者重放原计划／队列，复用原资源中性 terminal 验证核、native step 的 save／actor／tick／day／clock 验证和原 ledger rebind。仅接受原生已核实关闭、位置不变且无持久资源变化的安全对话；剧情或关闭回调带来的额外持久效果仍保留实际断点，不能归一化消失，也不从最终目标排除。
- 原 API 限定六文件交付 `f19b3e15`，控制端审查集成 `8626cf08`。其两次编译遇到 Core 显式 System 引用、测试误写 PlayerId envelope；已按上限停止交接，未执行测试、不可计为通过。控制端改正确 Value；首轮22项同一旧编译断点由诊断定位为测试遗漏原有 `menus.sleep_prompt_context`，仅补声明输入，不新增桥字段或放宽规则；原失败日志保留。
- 控制端最终聚焦31/31，扩大同一构建产物的相关回归790/790，零失败／跳过／构建警告／错误，重叠计数不相加。包含原执行入口的 HTTP 派发、四种丢确认恢复、不明原生结果不重派发、同一 owner 重开、原资源／位置／时钟／身份／菜单与工件篡改拒绝。旧晚间用例现核对安全对话先进入自己的控制恢复，不冒充晚间回家。声明／HTTP证据不是新游戏或完整托管通过，最终 head 仍须新 CI。
- **唯一接续与退出不缩减：** 已复核 #550 与原 R2：优先收口实际选定无清障 A 路径必需的终局／replan、原整波留存、所选销售／真实保存读回和 fresh bridge 连续性，再实际同一主循环恢复及有界 managed 原生验收。若该合法路径不含剧情／清障，不把全剧情或全清障扩为局部前置；实际首个中断按原证据处理，其他分支的所选效果／资源责任保留在原所属层。无新游戏／服务器／训练；L1冻结19/19、11/11，formal=false，local-data不提交。

## 2026-11-12T11:40:00+08:00 R2 晚间恢复复用原回家与睡眠

- 仍沿 R2 第3步，不改变剧情／对话优先级、普通模式或冻结 L1。新托管晚间选择只复用原 `GameClockBudgetPolicy` 的恢复窗口和当前可读 home：不在家走原回家 route，到家后走原 overnight／原生保存绑定，没有新增执行器、supporting kind 或生长规则。
- 中断返回原 `crop_teacher_late_day_recovery` 标记，并把实际 `RouteHome`／`NativeSleep` 交给既有日边界记录。清除被打断步骤的作物批次、购种／转移参数，不能把回家或睡眠当成原任务执行成功。route 保留旧默认 wrapper，仅托管晚间入口在复核时钟／更高优先级中断后调用原 route 编译器；原 route／sleep preparation、回执、账本／保存／恢复核不变。
- 本地接线 `5167f52a` 的已有相关74/74、源码守卫9/9通过；原 API 同会话限定两份测试交付8个用例，`d7221ccb` 审查集成 `67b80135`。初轮测试误用 internal 成员修正后28通过／1未过即退出交接，未继续付费修正。原因是原早晨夹具把睡眠候选 ID 写死；控制端改为从原编译队列读取实际身份，未改产品或放宽原回执验证。控制端额外猜测另一个 ID 的断言同样被拒，记录保留，不把猜测当权威。
- 最终当前产物相关82/82、零失败／跳过／构建警告／错误；与既有74和源码9项的范围不混算。同一原 owner 晚间回家→睡眠保存→早晨重开读回、marker丢确认恢复、未验证头／pending阻断、时钟／home／更高优先级中断反例均通过。两块未种布局仍为 unverified，不能冒充作物完成。其一日目录及命名保存文件仍是声明夹具，无新游戏／服务器／训练或真实存档读回通过。
- 前片 `46078644` 的 CI `43147930682` 暴露另一条旧源码断言：Core2599通过／1失败，仍要求通用决策租约恒 true。现区分新 owner／普通租约，保留优先控制只选择一次的约束；9项聚焦通过。最终发布 head 重新 CI，不借旧 head。
- **后续保持原范围：** 剧情／对话的所选原生控制结果、实际 site preparation／资源、终局／replan、整波留存／所选销售／保存读回与实际主循环恢复，随后 R2 有界 managed 原生验收。已查到原 output-retention 的全波次／共享生命周期及 terminal 的所选销售合同，接线时复用，不降成单次收获最低量或以普通控制冒充其通过。PR #567 仍 DRAFT，formal=false，local-data不提交。

## 2026-11-12T11:22:00+08:00 R2 原控制等待接同一 owner

- 沿原 R2 第3步接续，不重开冻结 L1。原 API 同会话在限定两文件交付 `9428a22f`，控制端审查集成 `13e682cd`；新增 preparation 显式 `nativeControlObservation=false`，只有 opt-in 才接原 `TryBuildNativeControlSettleQueueAsync` 的实际计划／编译队列。默认单连接器路径完全保留，没有第二套执行器或 supporting kind。
- 控制等待复用旧 `supporting_route_to_sleep` 的精确观察证明：原候选、30帧、原队列重放、回执、同日／原站位和持久资源不变。hop 是实际等待位置，最终目标仍是原家位置；在 Farm 等待不能宣称已回家，更不能记作物完成或 Teacher 标签。owner 的可选标记写入原意图哈希，重启仍用相同模式。
- 主循环的托管优先控制改从同一 owner 已接受头编译／登记，再走原 route native claim、原执行核心、一次 marker 和只读恢复；不借通用派发／决策租约绕开 owner。已存在工件不覆盖，pending 不再编译，不明 native 结果不重派发。未绑定的其他优先控制仍由原中央守卫在派发前停止留证。
- 聚焦39/39、零失败／跳过／构建警告／错误，覆盖实际执行入口的 HTTP 夹具、四种丢确认、未知 claim、同一 owner 重开和不误记回家。复用同一已构建产物扩大相关回归，最终760/760、零失败／跳过；两组有重叠，不相加。原 API 最终14/14，先前变量重名及 accepted-audit reason 误读已修；控制端两次夹具 using 编译失败保留，均非原生失败。
- 草稿首 head `c2778802` 的 CI `43144541959` 在 Backend 2305通过／1失败：旧源码结构测试仍搜索重构前执行语句，索引 -1 抛异常。现同时核旧派发与新 owner 的校验→执行→结算顺序及 managed 不重复结算，聚焦已通过；最终 head 必须重跑 CI，不能借父 head 绿灯。
- **唯一接续与草稿退出不缩减：** 接所选原剧情／对话／晚间恢复及 site preparation 的实际控制／资源结果，随后终局／replan、整波留存、所选销售、日边界／原生保存读回与 fresh bridge／accepted head 连续性，再同一实际入口正向／恢复及有界 managed 原生验收。当前只是控制等待接线通过，未新开游戏／服务器／训练；PR #567 继续 DRAFT，formal=false，#339/#550开放，local-data不提交。[专题](refill-approach-native-trace.md#2026-11-12-owned-native-control-observation)。

## 2026-11-12T10:50:00+08:00 R2 真实安装与主循环入口草稿

- #565 最终 `fced4ab6` 由 CI `43135815619` 全42步及同 head 无发现审查合入 `b2c5a8fb`，测试／main树 `8a6814ce` 一致后才清理发布引用。#566 将**已有**安装命令提取为返回实际 `StrategyLedgerEvidence` 的同一核心，最终 `16abb521` 经 CI `43141192487` 全42步／同 head 无发现审查合入 `4984c1d3`，核树 `d2a7afc1` 后清理发布引用。本工作树20项未提交接线逐文件 SHA 核对，快进父提交前后未丢失。
- 原 API 同会话独立交付安装三文件：14步、零API错误，22/22，原 upsert／journal／丢回复只读恢复核不改；控制器审查保存 `bd5bcc70` 并集成 `16abb521`，进程退出撤权、accepted-idle，无修正或重复构建。此小片已独立合入，不把未完成主循环混入其验收。
- 当前开发草稿显式 `--use-managed-crop-teacher`，仅单人校准／skip-training，默认旧模式不变。原初始 dispatch 经原安装核读回真实账本后打开同一 owner；完整原生库存／所有权要求复用旧 owner 读取器前置核验，缺失时在安装／owner创建入口的任何后端读写及托管目录创建前拒绝；上游原 dispatch 的 capture／源账本刷新范围不由此改称零操作。离线资源投影只用于可执行性预检，绝不作运行账本；仍用原请求、实际时间戳与读回结果。复用原 slot 隔离／backend-origin 检查，不新增桥字段或全世界门。
- 原 Program 入口增加 owner 租约管理、原阶段选择及原执行核心派发。购种／转移／播种仅增加 owner 内 runtime 路径覆盖参数，旧调用默认值不改；路由／浇水／收获／夜晚调用已存在的 owned adapter。支持阶段不再走通用执行回退，seed 的 Complete 与 Accept 各走原链一次。活动 owner 遇到尚未绑定的优先控制也在原生派发前拒绝，不能绕过 owner 去执行。
- 扩展旧／新相关720/720、零失败／跳过／构建警告；其后仅新增调度守卫与声明选择用例，最终聚焦4/4。含正常／丢回复实际安装启动、缺库存归属前置拒绝、默认模式、路由不回退、种子准备在 owner 内，以及同一 owner 跨日到收获选择。均为声明／HTTP夹具，单日生长不是原版规则，无游戏／服务器／训练。控制端初次 partial namespace 与夹具缺字段／固定 layout 路径误用的失败留存，修正仅相应接线／夹具，未放宽旧生产校验。
- **尚未完成，草稿不可当准入。** `nativeControlSettle` 等优先控制、剧情／对话、晚间恢复及实际 site preparation 仍未绑定 owner；终局／replan 保留原账本后明确停止。新入口尚未完成真实游戏验收、终局留存／销售／保存读回或整轮恢复；不是完整托管或开训。旧默认主线仍 unmanaged，L1冻结19/19、11/11，formal=false、#339/#550开放，local-data不提交。
- **唯一接续与草稿退出：** 首先把既有 `TryBuildNativeControlSettleQueueAsync` 的所选实际控制队列接入同一 owner，随后原中断／开路结果、终局留存／所选销售及日边界保存读回。核 fresh bridge 与 accepted head 的时间／中断衔接，沿原回执与只读恢复，不放开未归属执行、不再造控制器。完成同一实际主循环正向／恢复和相关回归后才将草稿转正式审查，再按 R2 做有界 managed 原生验收。[范围](refill-approach-native-trace.md#2026-11-12-managed-main-loop-entry-draft)。下方“顶层仍 unmanaged”描述旧默认及当时状态，不代表新 opt-in 已验收。

## 2026-11-12T09:26:00+08:00 R2 同一 owner 跨日到收获联调

- 本片续接 PR #565 草稿 `296c5be6`，仍属 [R2 第3步](FORMAL_FULL_TRAINING_READINESS_CN.md#3-接同一账本主循环和跨日恢复)。原 supporting 引擎增加明确 overnight 分类，复用原单次 NativeSleep 队列／回执、逐地块生长核、原保存指纹核、一次账本 marker、accepted index 和只读恢复。未另造执行器或生命周期算法，旧无保存绑定的 owner header 与水路格式保持。
- 保存根／slot 进入本 owner 的原身份；保存前指纹在 admission 固定，跨日和实际 slot 指纹变化都核实后才提交。证据／journal／native 输出不得与 slot 重叠，避免写证据制造 SaveChanged。保存未完成保留原 native claim，只读补查，不能再睡一次。保留的保存报告及哈希参与 outcome／acceptance，历史恢复消费原报告，不拿后续保存覆盖历史事实；完整原生存档读回仍是后续责任。
- 已从**同一个原始 owner 起点**联调浇水→回家→睡眠保存→回农场→收获并重新打开。11种新夜晚／收获 admission、native、保存报告、result、marker、acceptance 丢确认，以及保存未完成恢复均通过，实际 native 回调和每阶段账本提交各一次。未种布局的夜晚只记控制推进／未验证地块，不伪记作物完成；另覆盖错 slot、证据污染、重启换绑定和保存报告篡改。
- 原 API 独立只读核对提出有效P2：恢复证据输出漏用新 slot 隔离核。原失败保留实际 `marker-lookup.json` 写进声明 slot 的证据，首两例 teardown 的非空目录错误遮蔽断言，不把它当精确红例诊断；夹具现会保留异常工件而不遮蔽原断言。修正只在显式绑定保存的 owner 复用原隔离检查，覆盖 pending／已接受头恢复及种子准备恢复／续接的外部输出；旧未绑定路径不变。两项反例核目录未创建、无 lookup、原保存指纹不变，再走合法恢复，各阶段不重提交。最初交接快照漏共享文件，14步退出后在同会话补上下文一次10步完成审查，零API错误；进程退出撤权、accepted-idle，未第三次调用或倒灌完整输出。
- 初次相关203/203；审查修复后扩大到 owner 测试族，最终 **534/534**，零失败／跳过／构建警告／错误。新增20例使用明确声明的单日生长夹具，只证明接线／恢复，**不是原版生长参数或游戏托管原生 PASS**。首次路径／菜单事实装配不足及收获 helper 重建字段来源的夹具失败已修；生产资源守卫未放宽。无新游戏／服务器／训练，local-data不提交。本片尚待最终发布 head 的 CI／审查，不借旧 head 绿灯。
- **唯一接续：** 本片验收发布后，接原 `Program.cs` 的同一 owner 生命周期／实际派发、原收获留存与销售结算、原日边界记录／保存读回，然后做一次有界 managed 原生流程。当前顶层仍 unmanaged，不能把这20例当已开训。L1冻结19/19、11/11，#339/#550开放、formal=false。[专题](refill-approach-native-trace.md#2026-11-12-original-owner-overnight-to-harvest)。下方08:35的正向／丢确认待补已由本片声明夹具补齐，其原生与主循环未完成范围保留。

## 2026-11-12T08:35:00+08:00 R2 同一 owner 收获阶段与跨日前置

- #564 最终 `a251a5cc` 经 CI `43125520994` 全42步和同 head 无发现审查合入 `f786f9d6`；测试／main树 `bec82be7` 一致后清理自有发布引用。未提交 local-data，未启动游戏／服务器／训练。
- 当前收获接线在开发分支：原 owner 增加明确 harvest operation／admission／intent／index／native claim／outcome 分类，复用同一 supporting 派发、journaled marker、只读恢复与 advance。两个旧阶段的 bool 分类扩为内部显式三类；旧 water/route 格式、schema、文件名与 intent 字段顺序保留。原 due batch 条件提取同一核供 water/harvest 使用，Program 适配仍调用同一原控制编译与执行核心，没有第二套执行器。
- 原 API 同会话18步／零错误，限定三文件交付原逐地块 Crop.newDay 核提取及40项声明夹具；原全局生命周期继续使用相同投影、身份规则和错误文本，新叶只证明所选生长。控制端核对差异后保存 `5aa8a2f6` 并集成 `ecf4a573`；两条允许命令实际均 exit0，无修正或重复构建。进程退出撤权，accepted-idle；AGENTS.md 是私有工作树指令，不入产品提交。
- 控制端最终相关185/185、零失败/跳过、零构建警告/错误，含原 water/route、收获适配、生长叶及三项 owner 守卫。守卫证明：已有 watering 不可冒充 harvest、pending 不会再编译新阶段、不能把未来长好快照当原 owner 的当前头。**这些不是 owner 成功收获／原生托管通过**，当前分支应保持草稿，不能借旧水路的恢复测试宣称新收获丢确认已经实证。
- **唯一接续与退出：** 先把原 `NativeSleep`／`CheckCropTeacherDayBoundaryAsync` 的保存边界接到同一 owner 的实际头推进，并消费本次共享生长叶；随后做同一 owner 从原起点跨日到收获的正向联调，再验新阶段 native/marker/acceptance 丢确认不重派发／不重提交。保存未完成只能只读核对，不再睡一次；不能伪造长好后的初始 owner，也不以整轮 L1 生命周期证明作为每次睡眠的局部门。复用原保存根／slot、run/save/actor、队列／回执身份及恢复合同，保护其证据；原保存读回与整波 output-retention 的范围不由生长叶代替。
- 这仍是 R2 第3步内部接线依赖，不新增项目阶段或验收分母。完成上述正向／恢复联调后接原 Program.cs 主循环、原销售／原生保存读回，再一次有界 managed。顶层仍 unmanaged；L1冻结19/19、11/11，#339/#550开放、formal=false。[范围](refill-approach-native-trace.md#2026-11-12-original-owner-harvest-and-day-growth-handoff)。

## 2026-11-12T08:08:00+08:00 R2 原计划收获批次适配

- #563 最终 `3a0fda00` 由 CI `43121236133` 全42步及同 head 无发现审查接受，合入 `6b7c2388`；测试／main树 `49983bdf` 相同后才清理自有引用。旧211测试和 E 五帧资源重放按原范围复用，无新游戏／服务器／训练。
- 新 `CropTeacherHarvest.Preparation`／outcome 实现原 supporting 两接口。重放并锁定原计划及传递输入，核原存档／玩家／决策归属、active commitment、Day/BatchIndex、到期波次、输出／最低量、原布局有序子集及候选。沿原 ActionQueueCompiler 重放，不另建规划／执行／账本，也不把 harvest 的空 SeedQualifiedItemId 当输出。最低量来自原 wave.MinimumUnits/TileCount，不是诊断 minimum=1。
- 逐项核原回执、成功前缀、源状态／时间／路径，再调用已接受资源入口；未执行尾部不记完成，已执行阻塞尾部须无资源变化。沿原 SupportingRebind 产生请求并验证 reply，不把批次通过当整波 output-retention、生产归属或完整生命周期。原 watering 路径核抽取复用，行为／错误文本不改；原 planting 失败核保留种子字段必需，harvest 按其原合同可缺省，出现时仍比较。未改桥字段、旧 intent 或全局准入。
- 最终相关192/192、零失败/跳过、零构建警告/错误，含原浇水／播种与跨计划存档／归属反例。首轮嵌套私有访问编译错误及夹具 actor／当前地图／聚合／部分回执装配错误已修；最后一例只修新 harvest 调用误继承的种子必需条件，旧 planting 要求不变。声明夹具不是新原生证据。原 API 同会话13步／零错误、24行只读接线核对，无代码／shell／测试／提交权限；进程退出撤权、工作树干净，已 accepted-idle，未换 provider。[范围](refill-approach-native-trace.md#2026-11-12-original-harvest-batch-adapter)。
- **唯一接续：** 在原 owner admission／operation 分类中接本适配，复用 original supporting invocation、一次账本 marker、丢确认只读恢复与 accepted index，再接 Program.cs 同一主循环、原销售／原生保存读回、有界 managed。当前适配尚未接 owner／主循环，不称完整托管／正式训练；L1冻结19/19、11/11，#339/#550开放、formal=false、local-data不提交。本片待同 head CI／审查。

## 2026-11-12T07:34:00+08:00 R2 所选收获帧资源消费收口

- #562 最终 `8fb48259` 经 CI `43116326415` 全42步／Core2600／Backend2171零失败/跳过及同 head 无发现审查合入 `16b039ae`；核测试／main树 `79a8658d` 一致后清理自有引用。本片独立验收。
- 原拾取的 actor／图读取与数量汇总核提取复用；XML、入包顺序、对象身份、edibility 与未知元数据守卫仍留在原拾取入口，未降低旧输入要求。新收获物理存量消费者复用 `ExactInventoryReceiptVerifier`、原玩家槽位／容量／种子回显检查，核目标增量、原槽位元数据、外部保管与访问、全图汇总及货币；额外观察量不自动归本作物产出。种子类别 -74 已用同 pin PE 常量元数据核对，未执行游戏代码。
- 同一 `VerifyHarvestResources` 入口顺序消费原目标／技能／stock／社区中心比较，绑定输出与原目标；只还原已独立核实的比较值，再调用**原** expected-resource invariant 拒绝其他持久漂移。仅房屋材料镜像实际变化才复用旧检查，不为未变化字段新增门。字段级 JsonElement 引用复用，每个修改域只序列化一次，未复制世界或另建执行器。
- 最终相关211/211、零失败/跳过、构建零警告/错误，含原拾取与新增组合、种子回显反例。夹具是组件声明，不是新的游戏获取。原 E 五次收获通过同一强队列读取器／消费者各次完整调用：`SpecificPersistentResourceBoundaryVerified=true`，实际花椰菜0→1→2→3→4→5，货币不变；b报告 SHA `0ab7695b218b9b94b3d1178f5d572c4efe6dd5b816825d794d1f5bb4ca6684d8`。先前物理存量a报告 SHA `854a2b40294f59a597cba8b97ca8de1194f9897850b8c2b54d7ad8994521e330` 保留。末次加种子用例前后三个生产依赖 DLL 哈希不变且 Bootstrap 等于b记录，未重复已经接受的native重放。
- **证据边界不新增门。** 本次每帧 minimum=1 是诊断参数，尚未绑定真实 commitment／wave／owner；生产归属、私有 Item 全状态、原生终局与正式训练声明仍未作出。这些 false 不是新增的全世界 XML／生产证明硬门，按既有合同决定所选结算何时需要更强事实。当前只证明所选帧的已读资源效果，不能当完整托管、生产收入或模型更新。[范围](refill-approach-native-trace.md#2026-11-12-integrated-harvest-resource-consumer)。无新游戏／服务器／训练。
- **唯一接续：** 沿原 supporting-transition／owner 类型体系接收获适配，绑定原 plan.Day/ActionBatch、commitment／wave、原队列和回执，复用本资源入口与既有 output-retention 合同；再同一次账本提交、丢确认／中断幂等恢复，并接原 `Program.cs` 主循环。接续原销售／原生保存读回和一次有界 managed，不另建 owner／执行器，不回到逐项 L1 重验。当前顶层仍 unmanaged，L1冻结、formal=false、local-data不提交。本片待最终同 head CI／审查。

## 2026-11-12T06:58:00+08:00 R2 收获库存预览与实际进度分离

- #561 最终 `3c919775` 经 CI `43114142195` 全42步／Core2600／Backend2153零失败/跳过及同 head 无发现审查合入 `6894b961`；核测试／main树 `931d09eb` 一致后清理自有引用。当前片不借父片验收。
- 对同一 E1.6.15 SHA/MVID 做 PE-only 的 Bundle 两方法提取，合计1444字节、零诊断，前21旧工件及第二片包含的23工件逐字节不变。匹配属于原 native Bundle，候选由原 adapter 从 player.Items 生成，候选内的“完成献祭”等是未来操作预测，不是本次收获已经推进的游戏状态。本片不复制 matcher、不调用游戏代码／ItemQueryResolver，不新增 RNG 读取副作用；原读端风险仍按 #29 的既有责任处理。
- 新内部比较组件绑定两端候选的原始库存槽位、物品／运行类型、品质、stack／总量、ingredient index／所需量与当前未完成状态。只在收获专用比较副本里还原已知 `donation_candidates` 预览；实际 bundle bits、奖励、区域／邮件、材料配置、其他进度及未知字段继续走原 expected-resource 比较，未知候选字段拒绝。共享 normalizer／字段列表／旧执行器不改；不独立重建原生资格全集或未来结果，也不把预览作真实捐赠、产出或训练奖励。
- 新18例及原相关整合109/109、零失败/跳过、构建零警告/错误。原强队列5次收获独立重放：技能、目标及当前社区中心进度比较各5/5；原持久差异清单在这些组件之后为空。报告 SHA `27eda868c621b6b23c5d4c78830e2e18e7c2d4baca4336cc28a999feb909c14d`。**全资源边界仍为 false**：原合同排除的库存／材料图／货币必须由调用方结算，不能把余差为空当完整收获留存／生产归属／托管通过。[范围](refill-approach-native-trace.md#2026-11-12-harvest-inventory-previews-are-not-donations)。无新游戏／服务器／训练。
- **唯一接续：** 复用既有 `ExactInventoryReceiptVerifier` 与原作物 output-retention 合同，绑定原 commitment／wave 的实际库存留存和材料图／货币边界；额外拾取只按已证范围处理，不误归本作物产出。随后接同一 owner／账本／主循环、销售和原生保存恢复、有界 managed，不另建执行器或全局门。`Program.cs` 仍 unmanaged，L1冻结、formal=false、local-data不提交。本片待最终同 head CI／审查。

## 2026-11-12T06:34:00+08:00 R2 原收获作物转换核复用

- #560 修正最终 `55ca9670` 经 CI `43112255928` 全42步／Core2600／Backend2133零失败/跳过、同 head 无发现审查合入 `e57cb02e`。核测试／main树 `3f5ea0e8` 一致后独立清理自有引用。本片只发布其上的目标作物增量，未借父片绿灯验收。
- 接续 #560 技能片，原 `MoneyRevenueCropNativeLifecycleProofBuilder` 的单次／再生收获规则提取为同一私有核，旧生命周期调用与新内部目标消费者共用。复用原 CropState、catalog 和身份规则；无新执行器、目录字段或第二套生长规则。
- 目标消费者沿原请求定位唯一 live/ready 作物。单次收获只移除该行；再生仅按原生 Crop.harvest 与原桥推导更新 fully_grown、phase day、ready 和下一收获倒计时，其他作物、目标未知属性和外层元数据保留。按原生命周期范围只证明 Farm，不用同坐标代替其他地图。
- 本片20例与原技能／砍树回归，含 #560 missing-can 修正，整合91/91、零失败/跳过、构建零警告/错误。还直接调用原 `VerifyMoneyCropNativeLifecycleProof`，一次完成其既有生命周期、留存／销售／shipping、跨日／恢复及反例自测，正常退出；这是原声明夹具回归，不是新游戏证据。临时产物在仓库外 I 盘。
- 同一 E 强队列5次收获由新构建独立重放：技能镜像和目标作物组件各5/5；剩余持久余差每帧只有 `world_progress.community_center` 的库存捐赠候选回显。报告 SHA `18ea6f7b18d65fa9320870536839a961f6524cd6cd452e00569ea23c39ff4173`。原 native E 未重跑13日，无新游戏／服务器／训练。再生分支目前是 CLR／原夹具证据，不能算新的再生原生实跑。[范围](refill-approach-native-trace.md#2026-11-12-original-crop-harvest-target-kernel)。
- **唯一接续：** 对准原 Bundle 匹配／投影语义消费库存驱动的捐赠候选，并结算实际收获留存；随后同一 owner／主循环、销售／原生保存读回与有界 managed。组件不是生产归属、完整资源、托管或正式训练通过。#560 审查修正 head 仍须最终 CI；本片单独验收发布。L1冻结、formal=false、local-data不提交。

## 2026-11-12T06:20:00+08:00 R2 收获经验与升级镜像消费

- #559 文档片由最终 `455b9e7f` 全42步 CI／同 head 无发现审查合入 `879b5e3a`；核测试／main树 `38b2588b` 一致后才分别清理自有远端／本地引用。仍按 R2 第2步收尾接第3步，不重开冻结的 L1。
- 提取原砍树技能验证为同一内部 helper，仍用原 skill builder／等级规则；旧入口保留固定 foraging +16／请求端点，统计和地形守卫不改。收获绑定原请求／目标及桥已有经验投影。经验、buff、pending levels、评分和水壶／水盆／Mastery 镜像逐项核实；其他技能、职业、未知持久字段和 MasteryExp 变化仍拒绝。旧 expected-resource 字段列表／normalizer 行为不变，只增加内部委托入口。
- 首轮夹具64/64，但真实5帧均拒绝：新消费者／夹具误写 `exact`，原桥实际为 `exact_from_decompiled_native_harvest`。已对准原生产者并加短状态名反例；末次65/65、零失败/跳过、构建零警告/错误。a失败报告留存。新构建重放原 E 五次收获，技能／升级镜像组件5/5；b报告 SHA `5378281ceafd54a346a2a66c9138480af14e7aab9135f804caa2d781f654ea52`，每帧余差均为作物和社区中心库存候选。这不是完整资源／生产归属／managed验收；无新游戏／服务器／训练或13日重跑。[范围](refill-approach-native-trace.md#2026-11-12-harvest-skill-effect-consumer)。本片待最终同 head CI／审查。
- 原 API 本块两次均因指令路径发现权限退出，无代码／测试／提交；第二次进程已退出，任务权限撤销。停止本块重试、控制端接手；未来不同任务须先提供适用父级指令。授权保留空闲，未换 provider／内部代理。
- **#560 审查修正：** 水壶存箱时，原桥 `watering_can_missing` 仍投影玩家耕种等级。新增6例先4失败/2通过；现对准原生产者更新等级并保留 null 工具／费用与水盆状态，同时拒绝伪造槽位、费用或等级。最终相关71/71、零失败/跳过/警告。此缺水壶分支是 CLR 夹具证据，不能借旧5条带水壶原生回放宣称其实测通过。修正 head 重新 CI／审查，不沿用初 head 绿灯。
- **唯一接续：** 复用原生命周期作物转换核，结算目标作物及库存驱动的捐赠候选回显／实际留存，再接同一 owner 的收获、销售、保存读回和主循环，随后有界 managed。`Program.cs` 仍 unmanaged；L1 `19/19、11/11` 冻结，#339/#550继续跟踪、formal=false、local-data不提交。

## 2026-11-12T05:43:00+08:00 R2 原 owner 发布与收获合同收敛

- #557 的 `941f817d` 经最终 CI `43101653435` 全42步／Core2600／Backend2061、零失败/跳过和同 head 无发现审查合入 `57d88171`，树 `13c22120` 一致。#558 更新严格 base 后，最终 `1727bf5b` 经 CI `43103487742` 全42步／Core2600／Backend2084、零失败/跳过及同 head 无发现审查合入 `b1e2afb5`；测试／主线树 `4c737193` 一致。自有已合入发布引用已独立核验后清理。
- **发布纠正。** #558 首次被严格 base 门拒绝后，控制器未正确短路后续命令，误删未合入的 owner 引用、使 PR 被关闭。全部提交仍在子分支历史，已恢复精确引用和同一 PR，补 main 祖先后按新 head 重验、正式合入，无代码／测试／证据丢失。合并、远端 MERGED/merge commit/内容树核验与引用清理现分开执行，任何失败立即停止，不使用 admin 绕过。
- 原 E 的5次收获已复用原强队列／actor／nonce／journal/gzip／逐帧快照读取；同 SHA/MVID 的 PE-only Crop 3方法／9686字节、零诊断，19旧源工件字节不变，索引 SHA `606e11ec`。未启动游戏、服务器或训练。
- **拒收无效草稿。** 严格 projector 的原始差异不等于预期资源变化合同的阻塞。追到 `c31dfa72`、`5cc3b42c` 和 #497 原意后确认：原 `TryProjectExpectedResourceMutationInvariant` 已归一化奖券／工具／公会／邮件的容量回显，调用者另证实际资源变化。新增4类校验会无依据收紧旧 pickup；草稿 `582bcf97`／`b7d9f814` 只留私有 `archive/controller-capacity-mirror-draft-20261112`，不推送、不合入。其110测试与5组件实验不计当前产品验收，旧共享代码／可见性完全保留。
- 重新构建已接受代码，明确拒绝旧草稿二进制后，用原字段谓词及原 normalizer 生成 c 报告；5/5差异清单与原 invariant 判定一致。前4帧只剩 `farm.crops`、`player.skills_detail`、`world_progress.community_center`；末帧另有 `farm.pet_bowls`、`player.mastery_claim`、`player.watering_can_state` 的升级镜像。SHA `dabcf02e6fe8a0008f23638ed80365d4920a543656a730267a5f4d15dc4f77eb`，旧 a/b 及失败／草稿保留，不重标为 PASS。[范围和依据](refill-approach-native-trace.md#2026-11-12-original-resource-contract-correction)。
- **唯一接续：** 沿真实3类／升级后6类余差，复用原生命周期作物验证、技能经验规则与库存/集合投影，接收获留存和原销售／睡眠保存读回，再将同一 owner 会话接原主循环并做有界 managed。`Program.cs` 仍 unmanaged，无完整托管／生产归属／全局／formal新通过；L1冻结`19/19、11/11`，#339/#550保持跟踪，local-data不提交。

## 2026-11-12T05:03:00+08:00 R2 持久 owner 配置接原 preparation

- 同一 `CropTeacherManagedSeedOwnerOptions` 显式配置接入原 `PrepareCropTeacherOwnedWateringAsync`，仅实际补水接近才选资源模式，普通浇水不变。默认 false 不增加旧 owner header 字段；显式 true 绑定同一持久身份，重开时正反切换均拒绝，不能沿旧目录改模式。
- 准入前复用原工具输入门及 `NativeOutputObservationMode.Validate`；执行入口再次核原观察配置，准入后改坏配置也在 native claim 前拒绝。不要求无观察中性移动补 XML，不改执行器或旧默认守卫。[范围](refill-approach-native-trace.md#2026-11-12-retained-owner-resource-selection)。
- 原 API 同会话独立8项测试一次通过、无修正，`0ed1b383` 已审查集成 `73cdf3fc`。控制器补准入后配置变化反例；最终相关219/219通过，零失败/跳过、零编译警告。协作者已正常退出、临时 edit/bash/external 允许项均0，授权空闲。声明夹具不是新游戏证据；期间另一313项运行与源码编辑重叠，仅作辅助记录，不作当前 head 验收。
- 本接线纳入同一 owner PR #558，不另增第三条发布链；#557／#558 旧提交审查无发现，但 hosted CI 排队、尚无测试 job，不能借父片绿灯合并。更新后的 #558 重新按最终 head CI／审查验收。当前 `Program.cs` 仍走 unmanaged crop builder，不能把 preparation 可选模式说成顶层托管已完成。
- **唯一接续：** 完成本片发布验收，再将同一 owner 的会话生命周期和已验证路线接入原主循环，接续收获／销售／原生保存读回与中断恢复，随后一条有界真实 managed 作物周期。不另建 orchestrator，不在已准入后退回无主派发；原生所选效果和未结算来源继续保留边界。L1冻结`19/19、11/11`，fullmanaged／生产归属／全局／formal未证，#339/#550跟踪中、local-data不提交。

## 2026-11-12T04:48:00+08:00 R2 原 watering owner 的显式资源接续

- 本片接续 #557 的同一 trace，不新增执行器、账本或顶层循环。显式 preparation／`UseSelectedRefillResourceEffects` 使用独立 `refill_approach_resources` 意图和验收位；旧入口、默认意图和旧恢复规则不变。原 owner 仍复核完整来源、队列、日界、nonce、持久 claim／result 和单次账本提交。
- 新模式在派发前拒绝不支持的清障合同，并要求实际清障的原工具输入观察；中性移动不增加可选 XML 门。到达只是接近，不算浇水、作物进度、偏好标签或正式训练。支持的是完整原树合同／移动拾取与严格中性帧，不代表其他清障、清障期间入包或 Mastery 等效果已结算。
- 声明夹具覆盖原 wire、单次 native 请求／账本提交、native／marker／acceptance 丢确认恢复及新旧验收位防伪。整合相关313/313通过；末态快照复用优化后的53/53通过，零失败/跳过、零警告。复用已核对象而非再解析同一大快照，文件哈希／原守卫不减。这些不是新游戏或完整托管原生 PASS。[详细范围](refill-approach-native-trace.md#2026-11-12-opted-in-original-watering-owner)。
- #557 的最终 head `34877b27` CI `43096924399` 当前排队，未借父片绿灯合并。本 owner 增量另行发布／同 head CI审查；#339 被 #555 否定句误触自动关闭已查明并恢复开放，后续未完 issue 不使用关闭关键词。当前顶层仍 unmanaged，`PrepareCropTeacherOwnedWateringAsync` 尚不启用资源模式。
- **唯一接续：** 完成本片验收发布，再接实际顶层选择／观察配置和同一账本，补当前选中路线的未结算效果，然后收获／销售／原生保存恢复及有界 managed。原 native 六帧重放与前置间隙见上一片，不改称本片实战。L1`19/19、11/11`冻结，生产归属／全局／formal未证，#339/#550跟踪中，local-data不提交。

## 2026-11-12T04:31:59+08:00 R2 原补水 trace 统一资源消费

- #556 已合入 `310b5c61`，最终 head `69ee03b0` 与 main 树 `5bb1e827` 一致；CI `43089775742` 全42步、Core2600/Backend2036零失败/跳过，同 head 审查无发现，自有发布分支已清。下方“待审查”属于发布前历史。
- 新显式 `OpenWithSelectedResourceEffects` 复用同一原 trace／source reader／队列／journal/gzip／命名空间与到达守卫，逐原帧分为严格资源不变的移动、原合同完整砍树、实际拾取。零入包移动使用原完整资源 projector，不要求可选 XML；已请求但缺失的观察不能借此绕过。拾取经内部复用入口沿用同一 `ReadResourceEvidence` 对象，不第二次读取或新增 walker；旧入口和默认保持。[范围](refill-approach-native-trace.md#2026-11-12-selected-resource-trace-consumption)。
- 原 API 同会话两测试文件 `083cd042` 已审查集成 `592b6d4b`；首24通过／1重复中间状态 hash 的夹具错误，唯一修正只使中间站位不同、终点不变，随后25/25。新前基线165/165、独立 trace 分片末次32/32，均零失败/跳过、零警告。
- 原 native source 的独立 b 重放6帧：完整树／17单位拾取／2条旧中性移动共4条接受，2条旧清障因当时缺观察或实际工具输入保持 unavailable，不算 PASS。接受项均复用原证据引用，401tick前置间隙保留。`block18-selected-resource-replay-b.json` SHA `27cc24e3ebb8a0e59923c4c2370e5c470bf67cf03e34afce44f9085e91edad3c`；原 a 和既有失败留存，无新游戏。
- **接续与退出：** 本片只发布 trace 消费，单独同 head CI/审查；后续显式资源 preparation／同一 watering owner／丢确认恢复在独立开发分片中，不混为本片已合入。当前顶层依旧 unmanaged，尚无完整托管原生实跑、生产归属或正式训练。接续所选实际效果、同一账本与顶层，再收获／销售／原生保存恢复和有界 managed；不禁用旧开路／自动拾取来适配有限覆盖，未结算效果仍拒绝。L1`19/19、11/11`冻结，#339/#550跟踪中、local-data不提交。

## 2026-11-12T03:37:21+08:00 R2 原完整砍树的持久资源效果

- #555 已由最终 head `17f007af` 的 CI `43087459524` 全42步和同 head 无发现审查合入 `bb314d7b`；测试／main 树 `f0a4fb2c` 一致，Core2600/Backend1975、零失败/跳过。有效房屋阈值审查已回复解决，下方待发布记录仅属历史。本片清障增量随后单独 CI/审查。
- 本片沿 #555 的工具消耗／拾取资源核，不新增执行器或默认扫描。原 API 同会话交付目标地形组件 `2d875aa9`、39/39，控制器审查集成 `8bb9de0a`；仅原请求格被移除，其他有序地形／未知元数据完整保留。额外限定原 `farm.terrain_features` 只能证明 Farm，不能借同坐标证明 Town。
- 控制器消费旧 `wild_tree_removed` 请求和原 XP／统计合同，复用 `CurrentSkillExperienceDeficitBuilder`、技能等级规则、原水盆镜像及完整资源 projector。核实际 Axe 调用目标、体力、目标树移除、foraging +16、TreesChopped +1、跨阈值等级／待结算升级／总等级；其他技能、职业、玩家、统计、货币与未知持久变化仍拒绝。只对已经逐项核实的效果建立比较视图，不删共享字段、不要求 Tool 私有 XML。
- E1.6.15 同 SHA/MVID 有界反编译 `Farmer.gainExperience`：1方法/1728字节、零诊断、17旧工件逐字节不变。确认原生升级／待升级列表，以及满总等级后的 MasteryExp 额外效果；后者未被豁免，相关变化仍属未结算。外部索引 SHA `62da8405ccfb40f0052df6d8601271ed7f2fd47712ede104d7d7b04182fa7e4a`。最初命令行参数误传仅拒绝参数、无输出，后显式构建再运行成功。
- 新整合首61项为60通过／1夹具越界（把单槽夹具误当双槽），只修夹具；末次相关158/158、零失败/跳过、零警告。原生离线重放同一 clear/move 两帧：clear15次工具、270→240、目标移除／XP／统计／水盆镜像及原完整持久资源核通过；move17单位仍通过拾取图／镜像。外部 `block17-resource-replay-a.json` SHA `8b82f6ef8927fa1e9cc13ef3d38c4fddadddce234c314c31d111671aa8349773`。无新游戏、训练或成功矩阵重采。[专题](refill-approach-native-trace.md#2026-11-12-original-wild-tree-persistent-effects)。
- **接续与退出：** 把上述已验证结果接回同一补水 trace／账本和原 owned 适配层／顶层循环，再收获、销售、原生保存恢复及一次有界 managed。当前明确覆盖完整原生砍树且 clear 本身无入包、随后移动拾取的已验路径；其他清障合同、清障期间入包或 Mastery 等额外效果仍须依实际选中路线消费已有合同，不禁用原自动拾取／开路来迁就此分支。生产归属未明的拾取只作物理存量，不能误记本动作产出。主循环仍 unmanaged，完整托管／全局／正式训练未通过，L1冻结、#339/#550开放、local-data不提交。本片待最终同 head CI/审查。

## 2026-11-12T03:08:22+08:00 R2 实际工具消耗与拾取资源镜像

- **透明字段复核。** 菜单分支的对话输入已对准原 adapter 实际 `player.movement_timing_context.dialogue_up`，修正新增代码／夹具中的误名，不新增桥字段。末次拾取专项68/68，零失败/跳过、零警告；该增量重新接受同 head CI/审查。
- **2026-11-12T03:22:00+08:00 #555 审查修正。** 初 head `d89d8a71` 已通过 CI `43084616175`，但有效 P2 指出跨房屋材料阈值还会改变 `action_status`。9分支先红6失败/3通过；新镜像按原 adapter 的施工／金钱／材料／地点／柜台／店主／菜单与对话顺序重算，并核 before 状态，不能任选末态或删字段。修后相关168/168通过、零失败/跳过，原来源2条重放保持上述结果；b 报告 SHA `e0cbeed34dad75cbc94568754cceaa4cdaa96aedb2bb87cedf2e4f4b63928d8f`。修正 head 仍须独立 CI/审查，不沿用初版绿灯。
- #554 已合入 `bb4abe27`，测试 head `8b2b76d7` 与主线树 `3622ab08` 一致；最终 CI `43078519411` 全42步通过，Core2600/Backend1904、零失败/跳过，同 head 审查无发现。下方“待合入”仅为发布前历史，当前代码与远端已核一致。
- 仍按 [R2 第2步](FORMAL_FULL_TRAINING_READINESS_CN.md#2-用现有补水队列收口清障与拾取的结算证据)。实际工具消耗消费者复用原 `ReadResourceEvidence` 和工具扣减投影，逐次核原始体力端点／入口／赋值／出口，不重算公式或排序。原生嵌套调用继续在旧校准入口合法，新平坦消费不伪造其可结算性。时间间隙明确记录，不要求统一为零。
- 拾取消费者复用原槽位核、材料图读取器、原资源边界与镜像检查；玩家槽位、可用量／来源槽数、新槽元数据、容量和房屋所需材料数量逐项对账，外来库存／权限／锁／未知字段不豁免。仅在比较视图还原已独立核实的房屋镜像，原快照及哈希不改；货币与尚未投影的 seed echo 变化仍拒绝。不是账本提交或生产归属。[详细范围](refill-approach-native-trace.md#2026-11-12-native-consumption-and-pickup-resource-mirrors)。
- 相关157/157通过、零失败/跳过；首次86/86有1条新 nullable 警告，修后相关构建零警告。原来源离线重放2条：clear15次工具、270→240体力连续性通过；move17单位材料图／镜像和其余原持久核通过，401tick前置间隙保留。外部 `block16-resource-replay-a.json` SHA `7bd5fcabb1d5f3fa438ff45cbb32e1b9c9f0c3362e6ae07126e36602ce29a893`。没有启动游戏、训练或重采成功矩阵。
- 原 API 同会话29项专项通过；提交前被准确命令的权限匹配拦住，控制器审查后保存 `3104c196`、集成 `c8a87612`，未付费重试。peer 已退出、撤权、授权空闲；本片待最终同 head CI/审查。
- **接续与退出：** 可选完整 XML 拒绝斧头 `swingTicker/currentParentTileIndex` 是校准诊断，不是旧资源账本硬门，不为此新增工具动画观测。下一片用已有真实消耗与原清障目标核地形移除、经验／统计和必要镜像，再接同一账本／顶层 owner、收获／保存／有界 managed；无法归因的掉落不误记生产。顶层仍 unmanaged、完整资源／生产／正式训练未通过，L1`19/19、11/11`冻结，#550/#339开放，local-data不提交。

## 2026-11-12T02:02:33+08:00 R2 原入包载荷的槽位消费

- **2026-11-12T02:27:18+08:00 审查修正。** #554 初 head `e04e7ac1` 虽通过全42步 CI（Core2600/Backend1899），审查正确发现连续同类入包可借后一次 XML 替换新栈创建者元数据。反例先红1/2；现按原生顺序逐次合并兼容旧栈、再占第一个空槽，不能从末态任选见证。相关104/104、最终定向26/26通过，错误的“一次入包拆成两个未满同类栈”夹具一并纠正；修正 head 须重新 CI/审查，不沿用初版绿灯。最终 `block15-pickup-replay-d.json`（2条原来源）SHA `127de9475a93c32e05773a25f1b567c41d3c65dd195db69e14087d2e527ea6b8`，move17单位/capture前401tick间隙/clear工具状态拒绝不变；c中的旧未请求对照与早期工件保留。
- #553 已合入 `d7cf8d3c`，与测试 head `0fc9a262` 树一致；CI `43066397745` 全42步通过，Core2600/Backend1875、零失败/跳过，同 head 审查无发现。主/peer 已同步，旧自有引用和原 peer 补丁经保存与内容核对后收口。
- 本片 `feat/native-pickup-inventory` 待最终 CI/审查后合入。`ReadPickupInventory` 与 `OpenWithPickupInventory` 消费原入包 XML、原完整槽位载荷、原数量核、同一 trace 和工件保护，不新增游戏扫描、默认观察开关或执行器。原生堆叠保留已有栈而非要求输入整份 XML 相同；新槽需实际 XML 见证，已有槽的引用/完整持久和原始元数据不得随意变更，末态要求同 tick。原旧入口不改。[专题](refill-approach-native-trace.md#2026-11-12-pickup-inventory-capture-consumption)。
- 相关151/151通过、零失败/跳过，覆盖同 key 不同输入元数据、原生 receive-history 提前返回、新槽/跨槽入包、原始槽位错配、末态时间间隙、repair/no-repair 原队列和工件防篡改；随后入包 XML 深度/节点预算复用旧核，定向21/21通过。两次新测试夹具编译错误已修正并留日志；未改旧生产守卫或扩大超时，未重跑全量本地套件。
- 复用原 native source 的最终 c 重放：真实 move 的木材3→15/树液1→6共17单位通过完整 capture 槽位核；两条未请求载荷的旧对照为不可验证，不算 PASS。clear 仍因斧头 XML `currentParentTileIndex`189→191、`swingTicker`8868→8883拒绝，不能直接删字段。move 的 frame-before1876/capture-before2277 有401tick前置间隙，结果明确保留 `BeforeCaptureGapTicks`，不是全 frame、生产归属或完整资源通过。外部 `block15-pickup-replay-c.json` SHA `a16c8ff8cce1fc25429aebe80eaece753772e62a74fad5d222218698e9e9b64e`；早期 a/b 及失败均保留。
- 原 API 同会话只负责独立原生源码小块；首轮8步正确拒绝并不存在的 `Object.canStackWith`，末尾空结果/无报告不计完成。控制器核实实际继承 `Item` 后完成7方法/13471字节有界提取，原8工件不变，未付费重试/换 provider。`OnItemReceived` 的任务/邮件/统计等副作用未因此获豁免，协作者退出并撤权、授权空闲；原件不入 Git。
- **唯一接续与退出：** 仍按 #551/R2 第2步，将已定位的原工具状态变化与实际调用/消耗对应，再核材料图、容量、房屋库存镜像和未知持久变化，接同一资源账本；不是扩展所有私有字段。随后第3步把既有 owned 适配层接原顶层循环并收获/原生保存，再第4步有界 managed 实测。当前顶层仍 unmanaged、#550/#339开放，resource/full-state/formal=false、L1`19/19、11/11`冻结，local-data不提交。

## 2026-11-12T01:06:16+08:00 原 owner 的补水接近子阶段

- #552 已合入 `8d2d2d24`；最终测试 head `078de6a4` 与主线树一致，CI `43058702815` 全42步通过（Core2600/Backend1834，零失败/跳过），同 head 审查无实质问题，自有分支已收。初 CI 失败仍保留，不以初版审查代替最终验证。
- 新显式中性接近入口复用原 compilation、逐帧原生读取、完整持久资源 projector 和同一 watering owner/rebind。只接受原编译确实无清障、每个 native frame 资源不变的路线；未开启额外观察时不强加深层校准字段，已请求的观察仍必须通过原库存分母核。清障在此分支派发前拒绝，原 planner 的清障能力/预算不关闭；这不替代有清障或拾取路线的结算。
- 仅新阶段写 `maintenance_control_kind=refill_approach` 和 `native_refill_approach_verified`，不改旧普通浇水 intent 的序列化。到达不等于补满水或浇完地：crop count/tiles 为0/空，batch 未完成、preference/formal=false。原 claim、原 execution loop、marker/journal 的三处丢确认/只读恢复、下一浇水队列及防伪标记已联调，末次相关79/79、零失败/跳过。全量1874通过/1旧 Museum 15秒超时，随后该组14项在79项中全通过；未扩大超时或修改 Museum，共享超时根因未定，最终 CI 另核。新 outcome 直接复用 trace 的制品保护，不再第二次解码/哈希同批快照。[专题](money-crop-refill-control-outcome.md#resource-neutral-approach-on-the-same-owner)。
- 原 API 同会话独立交付三测试文件 `83bc55e9`，已审查集成为 `011ec3ca`；22步、零 API 错误、正常退出并自动撤权。29新增加2既有测试通过，单次测试命名空间错误已在唯一修正内解决。最大上下文92607，修正后的提前压缩配置保留；当前授权空闲，不换 provider 或会话。
- 旧原生 `output-client-native-b` 的原 projector 对照有2条 move 持久/库存/货币相等、2条 clear 持久变化拒绝。外部 `block13-neutral-resource-replay-a.json` SHA `79f41fca4a4b18398393c6231c6faed45b626aa38f36328484557a6731ce9269` 留存原来源/hash；不是新 owned 回执或 A 实战。此前17单位拾取与清障仍不能借此获得生产 credit。
- **接续与退出：** 完成本次 owner 子阶段的最终同 head CI/审查及发布，再继续所选清障/拾取资源边界，并将已有 owned 适配层接到原顶层循环，随后收获、原生保存和一次有界 managed 验收。当前顶层 dispatch 仍明确走 unmanaged，不能把 helper/声明夹具通过当作全主循环已托管。R2 既定顺序、#550/#339 开放、L1冻结 `19/19、11/11`、resource/full-state/formal=false 和 local-data 不提交不变。

## 2026-11-11T23:54:09+08:00 R2 所选补水路线的资源证据接续

- 已同步 #551/main `74990fea`，按[R2 第1-3步](FORMAL_FULL_TRAINING_READINESS_CN.md#r2-开训前执行顺序)施工，不恢复旧“先补全所有 held/offered/世界私有字段”的排期。#550 的事实与当前代码核对相符：深层校准默认关闭、未接资源结算；它是有界验证建议，不新增通用训练门或第二主线。
- 原 API 同会话用12步完成独立来源核对，零错误、正常退出且自动撤权，当前 accepted-idle。两个固定启动阻塞仍同时存在于共享列表，正式报告与 launcher 共用；未改锁。实际全局 OR 分支尚未由本轮引用的工件确定，记未判定，不用文档示例猜选中路线，也不因此阻断当前作物接线。
- 新 `OpenWithResourceEvidence` 复用原补水编译/同一 trace、原严格库存连续性核和原工具输入核，保留逐 admission 实际增量、顺序、scope 起始引用状态与可用的实际工具调用。旧入口、旧守卫、执行器及候选池不改；没有第二 walker/verifier。新 ID/返回 true/最终涨库存均不产生生产 credit，所有增量保持未结算。[专题](refill-approach-native-trace.md#2026-11-11-selected-route-resource-evidence)。
- 相关 Backend161/161、零失败/跳过；声明夹具验证 repair/no-repair 原队列接线而非游戏 PASS。最新外部重放 `I:\StardewAI-Collab\resident-refill-20261108\block11-resource-replay-b.json` 复用8个原生观察区间，7通过、1按 #541 数量间隙拒绝，另2个未观察对照不计 PASS；共34入包单位与15工具调用，source/run/save/actor/queue/nonce/tick/hash已留存。一个17单位正例深校准开关关闭，另一17单位正例开启；均未授资源/生产归属 credit。没有新游戏、托管周期或训练。
- 该 b 重放工件 SHA-256 为 `5f66f102295bc0a01ec5c195e3f2f2251aa5c56e7c10111f517523784f272f3a`；旧 a/历史失败保留，不覆盖或重标。最终同 head CI/发布在 PR 收口记录，不沿用旧版本绿灯。
- **2026-11-12T00:27:42+08:00 验证补记：** PR#552 初 head `ddd59a80` 的 CI `43053842387` 为 Core2600通过、Backend1832通过/1失败；失败发生在旧 owner 用例退出时的 `FileSystemWatcher.Dispose`，不是资源断言。未吞异常或重试伪绿：新增真实宿主配置断言先红，随后仅测试进程通过 runsettings 在建 host 前关闭无用 JSON 热监视，7项定向及 Backend1834/1834通过、零跳过。生产配置/执行器不改，最新提交仍须重新通过同 head CI。[配置依据及范围](refill-approach-native-trace.md#test-host-validation-follow-up)。
- **下一步及退出条件：** 本片审查/CI发布后继续同一路线的完整持久资源/槽位、实际消耗与需结算的因果来源，复用已有 guard/projector 接同一 owner；未知漂移仍拒绝，无法归因不伪记生产。然后收获/原生保存及 #550 A 的单一有界 managed 实战。B 已有数量正例/间隙反例，但字段必要性、完整生产归属与 A 仍未判定，不能把这些统计写成第2-4步整体完成。L1`19/19、11/11`冻结，full-state/nativeoutcome/resource/formal=false，Student21/21训后；local-data/raw不提交。当前周61%起续，停点改为约80%USED。

<!-- audit_invalidation: scope=2026-11-09_debris_next_steps; invalidated_at=2026-11-11T00:52:08+08:00; superseded_by=FORMAL_FULL_TRAINING_READINESS_CN.md#r2-开训前执行顺序; reason=原摘要未区分观测校准、账本消费和正式启动条件，容易把深层字段读作共同前置; historical_only=true -->
> **2026-11-11T00:52:08+08:00 接续修订。** #549 已合入 `a4cb6b43`。本轮仍沿同一条作物托管主线，先核对所选补水路线的资源结果，再接同一账本、收获和原生保存，随后按阶段验收。下方 #549 段的原生记录与回归结果保留，其「唯一下一步」排期由 [R2 开训前执行顺序](FORMAL_FULL_TRAINING_READINESS_CN.md#r2-开训前执行顺序) 取代。

## 2026-11-09 R2掉落物原始状态（当前唯一接续，本地收口）

- #548末head `70ceed36`经最终42步CI `42531314358`（Core2596/Backend1781，零失败/跳过）和同head审查合入`8eaaad03`，测试/主线树`a10cefcf`一致；主/peer同步，三份helper blob逐字节保留，第四份仅增加17行Hat分支，原补丁外部留档，自有合并分支已清。原生图与重放证据已收口，不重开旧矩阵。
- 继续原世界/生产来源缺口，先补原Debris/Chunk读取的明确瞬态和物理字段。对同一E1.6.15 DLL重新反编译并做只读元数据核：Debris29、Chunk16声明实例字段；43个非抽象Character类型和7个Debris枚举值来自实际图像，均不是动作/缺失字段计数。NetPosition.Value/Get/Paused可消费暂停事件或取消插值；NetFarmerRef.UID会在未定义时隐藏实际uid，新侧车使用真实底层字段，不改旧桥/行走器行为。
- 共享合同定义显式请求、原始浮点码元、空值可用性、物理引用、暂停事件顺序/时间戳和两端/回调共用上限；复用原字段/文本工具，保留旧私有入口及异常行为。提取后源码守卫查旧位置先红1例，修测试定位而非放宽产品；独立18项字段/文本核通过。原API同一会话三文件helper `efa57fbf`集成`24ec3414`，首轮原生CLR95项通过，实际getter陷阱确证读取不会消费暂停队列。Codex完成原adapter/消费核/CLI，复用同一图/XML与引用字典，不建第二walker。[合同](native-debris-state-observation.md)。
- 当前相关Backend378/378、Core10/10通过，零失败/跳过；E harness/bridge/backend/CLI/外部probe/replay构建零警告错误。新测试曾误以为重复Chunk可准入，实际旧receipt guard已拒绝；仅改为保留原身份拒绝的防回退测试，未修改旧消费守卫。新模式默认关闭，旧held-item getter求值顺序及所有旧unsupported guard保留。
- 隐藏静音、单逻辑处理器实际 `output-client-native-debris-state-a`完成原tree/move：raw Debris48/48，Chunk出现107次全核不变；图/附魔各4/4，原XML56/56、Item/选择54/54。tree两端行0/19、Chunk0/57；move两端行11/1、Chunk20/3，加17次收集回调/27个Chunk出现。当前source/数量/工具/XML/锚点/图/新raw消费者全链重放2/2；实际暂停队列均空，非空/别名/极值仅CLR覆盖，不能混算。原档SHA未变，game13280/backend7060均退出，夹具注入不是赚取资源或生产来源证明。
- 协作者22步、零API错误、正常退出；任务accepted-idle、授权保留，同一会话不消耗空闲轮询。专用最终生效edit/bash/external允许项均为0，启动器新增进程退出自动撤销本块授权，撤销失败blocked-idle；离线保留其他配置/幂等/拒绝未知agent和idle启动门测试通过，无付费探针或重试。旧账期暂停已被本次续用授权取代；美元余额未知，不用Codex周用量代替。
- **唯一下一步：** 完成本块同head审查/CI与提交发布；然后接原世界held/offered Item图、生产归属、before-capture和被动间隙证据。GPU内容、网络传输内部/委托闭包及其他子型/全世界仍明确待证，旧重复Chunk身份拒绝不放宽。齐后唯一资源owner、收获/保存跨日/managed、原全局协调及Provider/模型/manifest。L1`19/19、11/11`冻结，full-state/nativeoutcome/resource/formal=false、Student21/21训后；原raw/存档/夹具外留、local-data不提交，无正式训练/服务器部署。

## 2026-11-09 R2嵌套物品与附魔图（历史，已由#548收口）

- 上块#547末head `783da828`经最终CI `42521758426`（Core2596/Backend1748）合入`0e428a16`，核对测试树与主线树一致；不是沿用首head绿灯。原Object四字段证据已收口，不重开旧矩阵。
- 本块共享合同`d5f12bd3`、原API四文件helper `b8f637aa`集成`1337e05b`；Codex负责同一observer/引用ID/原XML消费核的adapter、CLI和测试。复用原GraphCheck而非另建遍历器；根原始状态留原槽位、嵌套状态进节点，先全图raw再header/XML。附魔应用字段与实际角色成员关系分别记录，不推断owner。
- 隐藏静音单逻辑处理器的实际`output-client-native-inventory-graph-a`完成原tree/move；4次图+4次附魔原字段/返回值对照全通过，原XML与Item/选择探针各54/54。两端实际节点13/13及14/14、唯一边14/14及16/16；实际重复访问19/19及20/20节点、18/18及20/20边，未按唯一表减预算。全部六类子边、空附件、Hat空位/附魔共享、held/input与Ring物理别名、两位置角色成员均实测；当前严格消费者/原source-数量-工具-XML-锚点客户端全链2/2且强制正样本模式。原档SHA未变，双进程退出。夹具是显式注入的隔离测试物品，不是原生获得或生产归属证明。详见[图观察合同](native-inventory-graph-observation.md)；最终相关回归/发布另记。
- 原API在具体夹具阻塞处正常退出；控制端修正夹具漏接NotSupportedException后83项通过。实际元数据还发现非Tool的Hat.enchantments，已只补新观察模式，另6项原生CLR增量通过；这两组不是同一次游戏实测。初77项边界/客户端后，最终相关Backend331/331、Core源码守卫6/6和最新E运行时构建通过，零失败/跳过，末构建零警告/错误。真实图实测/重放见上；最终同head CI与发布见对应PR收口记录，不沿用上块绿灯。
- 协作状态已分离为授权、进程与任务验收：旧账期额度暂停不覆盖11月8/9日明确恢复；同一OpenAI API会话真实成功。启动器核对当前任务的精确权限与生命周期；一次任务结束先awaiting-controller，控制端判定后才可续下一块，不自动轮询/重试。本次只读互审6步无新实质问题；随后API独立交付外部夹具、原生源码依据与唯一一次编译，20步正常退出；Codex并行接脚本/重放后负责真实运行。两轮零API错误，现accepted-idle，临时写/build权限已撤销。DeepSeek/OwlAI/内部子代理不启用，API美元余额未知，不用Codex周用量代替。
- **唯一下一步：** 完成本块最终相关回归、提交/CI发布；再按原序处理世界/生产来源/before-capture及被动间隙，齐后唯一资源owner、收获/保存跨日/managed及原全局协调/Provider/模型/manifest。当前仅21物品族/44附魔族的指定图子集，不冒称全部子型字段或全游戏已完备。其他子型/未准入物品族及完整状态缺口必须在后续准入前继续显式保留，不能被此图标记覆盖。L1`19/19、11/11`冻结，full-state/nativeoutcome/resource/formal仍false、Student21/21训后；无local-data提交，无训练或服务器部署。下方为历史。

## 2026-11-09 R2 Object原始子型状态（历史，已由#547收口）

- #546最终`ee93ba5e`经CI `42517352221`（Core2596/Backend1734，零失败/跳过）合入`e9e56d74`，核树`feb3fe5b`；主/peer同步，已核自有合并引用后清理，无文件清理。
- 最新原API源码合同已按实际E DLL刷新/抽核：Object四字段是真实省略状态；已有XML包装字段不重复补。旧停用被续用授权取代，专用agent收紧默认权限；报告路径误拦时立即退出，原补丁由控制端恢复，无重复付费核验。
- 本块生产/消费分工不重叠：原API只改原helper与新增Object字段文件，`3a4ca38e`集成`f3dc5840`；Codex复用原消费核、DTO/adapter和测试。新严格范围要求两端全部适用Object的四字段，缺失拒绝；旧12字段记录不冒充新子型证明。[合同](native-inventory-object-transients.md)。原生CLR84、消费266/Core26通过；初测试nullable警告已修。四端Release/探针/重放构建零警告错误。
- 隐藏静音实际a完成原tree/move，28/28只读probe含4次普通Object返回值直接对照，原字段/引用及有界RNG指纹未变；两实际记录经新严格字段核/原来源-数量-工具-XML-锚点客户端全链2/2，明确新feature。Object数量分别0/4，仅move是Object实测，tree空域不晋升。实际四值false/false/0/10，其他值和ColoredObject仅CLR夹具证明。move有16收集回调但非生产归因全证据；原档未变、游戏/后台退出。互审/远端CI与发布另记，无训练credit。
- 原API一次接口互审无新实质耦合意见；首head `bfba26e0`远端CI通过。控制端进一步发现可选第7参改变旧二进制签名，先红复现，再保留原6参入口薄转同一实现，14项Object/ABI通过；不改原观察守卫/数据、不重开游戏。末head CI另记，不把首head绿当成最终证据。协作4文件包完整哈希归档后从81752缩至7607字节，只当前指令生效；源码/成果未删。
- **唯一下一步：** 完成本块原生读取与完整链验证/提交，再复用原GraphCheck/引用/本helper补嵌套Item和附魔应用/所属；世界/生产来源/before-capture与被动间隙仍在唯一owner之前。L1`19/19、11/11`冻结、formal=false，Student21/21训后，local-data不提交。下方为历史。

## 2026-11-09 R2瞬态文本保真收口（当前唯一接续）

- #545最终`1d1faa0a`经42步CI `42509339649`（Core2596/Backend1726）合入`87e1f1ef`，核树`c024778f`，主/peer库已同步同版本，已验内容后清自有引用；无文件清理。
- 同API生成的实际DLL元数据索引修正本地重复mscorlib解析后完成：21物品根/44附魔类型/67类型/477声明字段，其中243静态、234实例，12为当前根侧车读取；不是“还缺477项”。失败索引/新完整索引分别留外部，未调用游戏构造/属性或读取字段值。
- 本片发现异常UTF16经JSON会从原始55296替换为65533；生产/消费共用纯码元校验，不能保真就明确不可验证，不静默归一化。65536单码元对照、有效控制符/CJK/代理对、40原生CLR生产端和7类消费者回归通过，相关Backend275/275；四端Release及重放构建通过，原真实记录在新核下仍2/2，未重开游戏/178矩阵。[合同](native-inventory-transient-observation.md)。
- **唯一下一步：** 本片验证发布后，按已完成索引实时核Object/ColoredObject控制字段和BaseEnchantment所属/应用状态，复用原图/引用/observer补嵌套及其余相关子型状态；再世界/生产来源/before-capture与被动间隙，齐后唯一owner，再收获/保存跨日/managed及原全局/Provider/模型/manifest。L1`19/19、11/11`冻结、formal=false；仍未开放正式训练，local-data不提交。API授权空闲非停用。下方为历史。

## 2026-11-09 R2原生物品瞬态与选择分支（当前唯一接续）

- #544最终`ec4e3669`经42步CI `42505475775`（Core2595/Backend1662）合入`62f7bfe1`，核树`ec46012a`，自有feature引用已清。工具等于选中槽位的审查建议被实际getter7例证伪，回复/补3回归后解决，不改旧原生/执行器。
- 同一API协作者刷新4类源码并交付两只读helper `eebbc53d`（接最新main后集成`806ec63f`，已核测试树一致），178原生CLR夹具/一次E构建通过；Codex负责DTO/原observer/消费者。新增默认关闭的Item临时字典/拾取flag/缓存、Tool使用者/声音及缓存、镐头私有3计数和临时/收起选择分支，空值/缺失/false/0严格区分。合同见[瞬态观察](native-inventory-transient-observation.md)。
- 同源互审无新实质问题；单物品文本总量漏限已先红复现再补，相关Backend265/265、Core37/37及四端Release零警告/错误通过。原总预算含XML和新原始文本；新模式先采原始缓存，ItemId需迁移则读取前拒绝，不改旧模式。
- 静音隐藏隔离a完成原clear/move：22次物品+4次选择读取，26/26通过RNG/有界原始字段/引用/tick不变核；原档未变、双进程退出。b重放明确新消费者2/2，实际lastUser关系1→2后2→2，镐头计数原值均0；不是稀有分支游戏实测或资源证明。两scope均0收集回调，故不能晋升拾取/生产归因。
- 首次外部重放在构建未终止时误用了旧二进制并出现复制重试；新特征标记检查拒收，等终止后b重放才接受，原日志保留且未重开游戏。今后构建句柄未结束不得消费输出。
- 本片补齐同文本ID/不同物理Actor及GUID大小写碰撞两项回归后，最终相关Backend267/267通过；源码与四端原生构建/实际a校准一致，新增仅测试/文档。最终远端CI/发布见收口。
- **唯一下一步：** 本片收口后，继续同一trace剩余子型瞬态、世界效果/生产来源及before-capture/被动间隙；齐后唯一owner，再收获/原生保存跨日及managed有界真实集成，随后原全局/probe/Provider/模型/manifest。L1`19/19、11/11`冻结、formal=false，Student21/21训后，local-data不提交。API仍授权，同会话块边界空闲不是禁用。下方为历史。

## 2026-11-09 R2原生完整槽位持久载荷（当前唯一接续）

- #543最终`65d8a0a4`经42步CI `42496580856`合入`72dc6d1a`，核树`475c17f0`，Core2594/Backend1595；旧工具输入矩阵/L1不重采。
- 同一真实API会话提供原生live XML helper `fe74fbbc`（集成`dcfafcb4`），本片沿原observer加入默认关闭的完整槽位/选择/引用/原始XML载荷，复用旧writer和总预算。不是完整瞬态/世界或资源credit。[合同](native-inventory-payload-observation.md)。
- 实际隔离payload-a：23次原生共享读取全部通过RNG/有界状态/旧字节等价核，两条原来源/数量/工具/载荷/当前客户端重放2/2，原档未变、双进程退出。无新游戏重采。
- API互审两项P2已先以6例复现：伪造/未支持类型及错前缀可自洽通过。现生产/消费共用21类型合同，按真实Clothing标量等身份核验；原旧Object读取策略不改。扩展原生CLR夹具171通过；最终回归/构建/发布另记，失败诊断留档。
- 本片最终定向Backend200/200、Core36/36通过；四端Release及离线重放构建零警告/错误。加固消费者复用原两条真实记录仍2/2，另存重放报告不覆盖旧结果；远端CI/发布另记。
- #544审查建议强制当前工具等于选中槽位，原生getter7例证实临时物品/收起存在反例，不能照改；已补3例明确本片只保留原生返回、不证明完整选择分支，Backend203/203通过。临时/收起状态仍在后续瞬态证据内，旧执行器/原生getter不改。
- 协作授权已按11月8日/9日最新指令校正全局默认、skill和外部board；旧耗尽暂停不永久继承。本次原API任务成功并正常退出，同会话授权空闲，不用内部子代理、不做付费空轮询；周53%不能当API余额。
- **唯一下一步：** 完成本片收口后，继续同一trace的剩余瞬态/世界效果和生产来源（含before-capture/被动间隙），齐后唯一owner；再收获/原生保存跨日及managed有界真实集成，随后原全局/probe/Provider/模型/manifest。L1`19/19、11/11`冻结、formal=false，Student21/21训后，local-data不提交。下方为历史。

## 2026-11-08 R2实际工具调用输入（当前唯一接续）

- #542最终head`b587129e`经42步CI `42490952480`合入`ecb8fab0`、核树`a31449ec`，Core2590/Backend1542；部署漏依赖意见正确并已修复/回复。自有分支内容核对后清理，非文件清理，旧成功矩阵/L1不重采。
- 同一真实API会话第四块`1e5775b5`集成`3ce06ea9`：新可选工具输入记录复用原observer，精确记录base/局部power之后、原生setter/跳过分支及父调用出口，不改工具执行器/动画。默认关闭且只隔离校准，完整记录前不授权资源。[合同](native-clearance-tool-observation.md)。
- API原生IL/原子回滚53、合成生命周期65；Codex初89/35通过。互审引用类型混用反例先红，按GUID核同引用CLR类型、可变属性不锁死，最终相关Backend156/156；嵌套保留结束回调顺序而非排序入口ordinal。工具安装失败不污染旧collection状态。
- 真实斧头270→268/tick1171、镐头270→268/tick885，镐头原始参数1/实际局部power0，后续移动均0调用；四记录经原来源/库存数量/新输入核/当前客户端重放4/4，原档未变、进程退出。镐头A仅新夹具验收遗漏，B仅测试误要求普通Farm石头专项ready；C按原raw clear/实际槽位校准、无伪造产出或改旧桥门。失败均留档，不重采斧头/L1。最终Core38/38、四端Release零警告/错，CI发布见收口；非全物品/世界/生产来源或训练credit。
- **唯一下一步：** 本片验证发布后，同一trace补完整物品元数据/世界效果与生产来源（含before-capture/被动间隙），齐后唯一owner；再收获/原生保存跨日及managed真实有界，随后原全局/其余probe/Provider/模型/manifest。L1`19/19、11/11`冻结、formal=false、Student21/21训后，local-data不提交。API仍授权，同一会话块边界空闲非停用。下方为历史。

## 2026-11-08 R2原生观察/末态同帧锚定（当前唯一接续）

- #541最终42步CI合入`fed58419`，核树`43fd17e5`并清自有分支，Core2552/Backend1529。L1和旧成功矩阵不重采。
- 同一真实API会话第三块`baf62883`（集成`ad72e82d`）完成只读主线程fresh-full快照API，Codex补轻量引用和默认关闭的请求/观察收口/缓存读回。只显式校准move/clear在原observer结束前取同帧快照，复用原full缓存及after-hash/tick协议；默认工具/动画/移动不改。[合同](native-output-snapshot-anchor.md)。
- Core39/39、四端Release零警告/错误、初客户端35/35；同peer互审两项P2已核实修复：必须性改取真实派发request、独立核receipt末tick等于锚点，扩展38/38。初9失败仅新夹具int/long类型修正，旧ReadLong不改；依赖失败/审查如实留档。
- 静音隔离真实砍树→拾取applied/observed，拾取17次collection；原档未变、双进程退出。两记录经原source reader/完整库存数量核及加固客户端离线复用2/2，同帧2140/2140、3038/3038，每次只读一份精确缓存。旧2295→2297间隙记录仍拒绝、不重采成功项。非完整物品/世界/生产来源或训练credit。
- #542初head42步CI通过后，自动审查指出标准部署漏带新RuntimePrimitives依赖，意见正确；已补清单、依赖自身及桥输入新鲜度核，未改旧守卫。回归先红后4/4绿，隔离真实部署脚本核5文件哈希/config保留/API类型及两类过期构建写入前拒绝通过，无新游戏；最终head CI另记。
- **唯一下一步：** 本片验证发布后，同一trace补实际工具调用输入、完整物品元数据/世界效果与生产来源，含before-capture/被动间隙，齐后唯一owner，再收获/原生保存跨日和managed真实有界及原全局/其余probe/Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21训后，local-data不提交；API合作仍授权，任务边界空闲不等于禁用。下方为历史。

## 2026-11-08 R2原清障物理消耗/库存间隙（当前唯一接续）

- #540经42步CI `42481457238`合入`f95fd37f`、核树`1054cbf5`并清自有分支，Core2521/Backend1489。已验L1/干草矩阵不重采。
- 真实API协作者提交`e89214df`、Codex复核接入`38fd23b8`：Axe/Pickaxe单次原生体力赋值纯投影，区分实际power、有效技能、Efficient与祝福保护及float/setter限幅。31专项及整合Core65/65通过；不冒充实际调用/整动作/资源credit。[合同](native-clearance-resource-continuity.md)。
- 新快照库存边界复用原观察核和快照源核，完整ID/原始quality分母两端比对；相关Backend90/90。4实际观察的离线重用3通过、1明确间隙：树木拾取capture结束tick2295木材16，after快照2297为17，before/capture-before均5。原档/旧报告保留，不容差放行、不重采3成功项。
- 同一API会话第二块`29a73ac9`已接原trace强入口，每帧复用快照库存核；旧Open仍校准-only/continuity=false，新入口全验后才true，全部credit仍false。110/110通过（20新用例+90观察），原队列/源/journal/gzip/CAS全保留；两次新夹具失败如实修复留档，不改生产守卫。
- **唯一下一步：** 本片发布后处理原观察生命周期/快照窗口，优先复用已有fresh full快照/缓存及after-hash协议，不新造执行器、不靠等待/容差。实际工具输入、完整物品元数据/世界效果及生产来源齐后才唯一owner，再收获/原生保存跨日、managed真实有界和原全局/其余probe/Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21训后，local-data不提交。下方为历史。

## 2026-11-08 R2干草规范资源读取（当前唯一接续）

- #539经最终42步CI `42476060159`合入`5bf13fbc`并核树，Core2487/Backend1483。原6实际来源重放与L1不重采。
- 同一补水清障补直接入筒仓资源：`farm.hay_storage`为稳定非室内/非生成地点加Farm存量，`current_location.hay_storage_context`单列当前源/原生顺序；换图不污染持久资源核。旧执行器/资源核不改，纯分配不预测割草RNG或授生产credit。[合同](canonical-hay-storage-resource.md)。
- Core34/34、相关Backend355/355通过；原生B核87地点/24读、7分支/跨地点及patch拒绝恢复。B空元组留档保留，C只补完整双schema读留档，不重采7分支；原档未变、进程退出。发布见收口。
- **唯一下一步：** 本片发布后，同一原trace的精确clear/pickup资源与生产来源（含capture间隙、工具体力）接唯一owner，再收获/原生保存跨日和managed真实有界集成，随后原全局协调/其余probe/Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21训后，local-data不提交。
- **协作纠偏：** 用户11月8日重新启用赠送API，旧11月1日耗尽暂停已被取代；真实OpenAI常驻peer独立负责清障工具精确体力，Codex负责本片/资源接入，路径不重叠。授权/实际session/退出条件见外部 `I:\StardewAI-Collab\resident-refill-20261108\COLLAB_BOARD.md`，故障只停对应接口，不冒充内部子代理、不重试风暴。下方为历史。

## 2026-11-08 R2原补水清障/拾取的原生trace绑定（当前唯一接续）

- #538经42步CI `42473610429`及同head审查合入`9452e4b2`，树`f0bdb211`一致并清自有分支，Core2487/Backend1420零失败/跳过。原掉落73/单位792矩阵不重采。
- 本片消费同一原补水编译与原ReadQueue/ReadFrame/step边界，绑定完整queue/clear顺序、真实frame/nonce/隔离及journal/gzip；纯观察消费者核原引用、完整XML身份/品质/stack、逐次库存接收区间和总量/上限。已在scope前存在的掉落保持明确，新引用不冒充本次产出；原生capture到after快照的间隙仍须核。只读trace不派发/接owner/授资源credit。[合同](refill-approach-native-trace.md)。
- 原生留存重放发现旧wire reader误拒可空值字段的空占位→显式null；已核`3bde2048`及11月5日JSON原意，原mapper两例先红，仅修该等价，缺字段/非空/非可空/null字符串仍拒绝。初32/链41/扩45、修后54通过；6实际动作经原来源reader、4观察scope及16接收回放通过，原样本不晋升。末次相关/最终CI另记，无新游戏/worker/训练。
- 最终含7上限增例的相关Backend429/429、原来源/criterion Core212/212通过，零失败/跳过，重放构建零警告/错误；完整CI/发布另记。本片不要求已验根或原生矩阵重跑。
- 初head自动审查指出新消费者按库存值排序会接受颠倒历史，2例先红复现；现仅新消费者严格沿collection/admission记录顺序逐次核当前库存，零增量不能预用未来数量，不重排嵌套不明确顺序。末次定向/旧原生离线回放及最终head CI另记，初审与此增量分别归因。
- 此审查增量末次定向63/63通过，6实际来源/观察离线回放仍通过，重放构建零警告/错误；原429/212及失败记录保留。最终head CI/发布另记。
- **唯一下一步：** 完成本片发布，再用同一trace及原clear/output基础实现精确开路/拾取资源与生产来源边界（含capture间隙），接唯一owner；收获/原生保存跨日和managed真实有界集成，再原全局协调/其余probe/Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21训后，local-data不提交。下方为历史。

## 2026-11-08 R2原掉落摘要的普通物品读取纯度（当前唯一接续）

- #537最终head经42步CI `42471318373`接受，合入`086a24e1`、树`61e7b7f7`一致并清自有分支，Core2486/Backend1420零失败/跳过；初审与末次registry补丁增量分别归因。已验792单位矩阵不重采。
- 保留旧`792902669`为无held-item掉落补齐registry元数据的用途，只复用同一共享构造函数消除普通对象的无用抽样；实际held-item、所有摘要/chunk字段/品质原值、身份匹配和既有状态枚举不改。非零品质按同一E原生setter→FixQuality（3→4）处理，被patch则旧registry回退，零品质不新增调用。[合同](ordinary-drop-projection-purity.md)。相关/原生验证及发布见收口，非全桥纯度或资源credit。
- 相关48/48、两端E构建零警告/错误；原生A完成73普通/混合JSON等价及RNG/所核世界/实际物品不变、3特殊/错误旧回退，随后夹具打继承方法补丁失败。B确证继承反射方法漏检真实patch，修共享声明方法归一化；C/D定位旧registry已内联setter的0回调不能要求大于0。E定向通过setter/FixQuality同条件旧新摘要与回调一致、零品质跳过/撤patch恢复，另两端ItemId继承setter的standard/clone拒绝恢复通过；失败保留，不反采73/792，原档未变且进程退出。最终CI/发布另记，不是全桥或训练准入。
- **唯一下一步：** 完成本片发布，接同一原补水编译的实际清障/拾取资源与来源边界到唯一owner；继续收获/原生保存跨日和managed有界真实集成，再原全局协调/其余probe责任/Provider/模型/manifest。不反采已验矩阵或L1；L1`19/19、11/11`冻结、formal=false、Student21/21训后，无worker/local-data提交。下方为历史。

## 2026-11-08 R2原单位哈希的普通对象读取纯度（当前唯一接续）

- #536经42步CI `42466559070`及同head审查接受，合入`b27ee575`、树`3b3b94ba`一致并清自有分支，Core2480/Backend1420零失败/跳过。已校准原客户端传输及16次真实收集回调不重采。
- 核`f3133e72/6f21e240/e847e04f`原单位栈/翻转归零/库存历史与工具节拍语义；同一E源码确认普通Object构造抽样不会被翻转归零撤销。本片只共享固定二进制、精确类型且原方法未被patch的无抽样单位构造；原两端哈希、Cold FromStandard及原生fallback复用，未复制执行器、不换RNG或哈希格式。[合同](canonical-object-unit-purity.md)。
- 原生A/B对照：807行中773普通对象加19元数据变体共792输入与旧字节一致，34特殊类型保留旧路径；实际两端哈希/库存历史/异常及patch回退核验，RNG引用/完整状态及所核玩家/当前地图状态未变，原档未变、进程退出。只读RNG传感器留测试共享目录，不随产品运行；首4、最终相关47通过，最终验证发布见收口。不是#29全局完成或生产credit。
- 最终C矩阵保持通过。初head42步CI/审查通过后继续核新入口，补registry查询patch拒绝；定向D暴露Harmony撤patch后保留空info，只修新判定为实际owners而非info非空，E验证6次旧查询回调、哈希不变且撤patch恢复准入，相关47仍通过，未重跑已验矩阵。最终head验证/发布见收口。
- **唯一下一步：** 完成本片验证发布，不重采已通过单位矩阵；继续原补水涉及的普通registry掉落投影（旧`792902669`用途），保留摘要语义并核等价/RNG，再真实清障/拾取资源接唯一owner、收获/原生保存跨日及managed集成。其后原全局协调/剩余probe责任/Provider/模型/manifest。L1`19/19、11/11`冻结、formal=false，Student21/21训后；不启worker、local-data不提交。下方为历史。

## 2026-11-08 R2原客户端显式观测接入（当前唯一接续）

- #535经42步CI `42463019944`与同head审查接受，合入`6774d22b`、树`73290565`一致并清自有分支；Core2476/Backend1395零失败/跳过。已有观测出口不重做。
- 同一E二进制源码核实：空槽完整接收后传入Stack可不变，部分合并可返回false。新字段改为中性`item_stack_after_call`，分别保留原生返回、调用后数量和库存变化；不把它误称未接收余量。原客户端新增默认关闭`--observe-native-outputs`，仅校准单人harness/反馈模式，沿原请求工厂、journal和HTTP，为原move/clear加完整身份的有界scope；其他队列项/执行器/默认保持。[合同](native-output-observation.md)。验证/发布见本片收口，观测不授予生产或训练credit。
- Core22及最终相关Backend152通过，E四模块Release构建零警告/错误。隔离原生短校准：默认/显式clear-move四项及原砍树候选→真实掉落移动两项均applied/verified，后者实际捕获16次collect/admission（木材5→16、树液1→6），原wire/nonce/身份/tick/XML摘要已独立核验，原档未变、双Mod且进程退出。初启动漏参数和未覆盖回调的样本保留；不是RNG等价或生产credit，最终CI/发布见收口。
- **唯一下一步：** 完成本片验证发布，继续剩余物品/世界/RNG控制对照与#29旧路径按原意修复；已校准传输/回调不重采，再同一补水编译的真实清障/拾取资源接唯一owner，继续收获/原生保存跨日及managed真实集成。其后原全局协调/Provider/模型/manifest；L1`19/19、11/11`冻结、formal=false，Student21/21训后，不启worker、local-data不提交。下方为历史。

## 2026-11-08 R2显式原生收集观测（当前唯一接续）

- #534经42步CI `42459220203`及同head审查接受，合入`3270e2d3`，Core2458/Backend1395零失败/跳过，核树清自有分支。原补水接近编译证书不重做，仍不是原生效果证明。
- 本片复用原action-origin捕获，原执行API新增默认省略的显式scope；只原单人move/clear且原现场校验通过才观测，旧采集捕获白名单/歧义/需求量完成门不扩大。懒安装只读collect/admission钩子，弱引用身份、实际前后库存/掉落/部分接收记录，原任务完成后还须等最终捕获再回包；默认任务/错误语义不变。观测失败/类型不支持/超限显式unavailable，记录不授予native或训练credit。[合同](native-output-observation.md)。
- 当前E四类源码已重核。实际对象只读XML有总量上限，不调用getOne或创建物品；旧单位hash的构造器RNG路径已核调用点并补到[#29](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/29#issuecomment-6719205294)，旧helper未改，原上下文仍须追溯，不能丢旧规范化/哈希合同。首36、相关97、末次37通过，E原生构建零警告/错误；最终CI见收口，不冒充游戏或RNG对照实测。
- **唯一下一步：** 本片验证发布后，隔离有界原生/控制样本核钩子、最终回包及物品/世界/RNG无新增副作用；按原意审计修#29具体旧路径，再将同一原补水编译的真实清障/拾取资源证据接唯一owner。不松中性路线/浇水门，不反采L1；继续收获/原生保存跨日、有界managed真实集成，随后原全局协调/Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21为训后；不启worker、local-data不提交。下方为历史。

## 2026-11-08 R2原补水接近编译边界（当前唯一接续）

- #533经42步CI `42458156939`与同head审查接受，合入`a0be56cc`，测试/main树`704397be`一致，Core2458/Backend1381零失败/跳过。共享生产时需求量完成门不重做；其自有分支待本片提交接最新main后清理，不动他人分支。
- 本片只固定原补水接近的完整源/账本/到期承诺/原布局站位及原模型/编译队列；复用原supporting源核、retained/CAS守卫和原编译等价核，保留所有真实清障项ID，不借中性路线、不禁用旧开路。明确不实现原生phase准入/执行，native和formal均false，旧owner/执行器/默认不变。[合同](money-crop-refill-approach-compilation.md)。
- 原控制器+真实后端编译首14/14通过（含有/无清障），随后错误分支三项定向和相关160/160通过，零失败/跳过；最终CI见收口，非新游戏证据。
- **唯一下一步：** 本片验证发布后，以同一原编译接原清障/拾取的精确资源与来源边界，再接唯一持久owner；不得靠编译证书放行资源变化。继续收获归属/原生保存跨日，齐后managed有界真实集成；随后原全局日历/预算/跨根/carrier、Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student21/21为训后；不启worker、local-data不提交。下方为历史。

## 2026-11-08 R2共享拾取完成门的生产时归因（当前唯一接续）

- #532已由42步CI `42455296540`及同head审查接受，合入`c0717ae3`，Core2443/Backend1381零失败/跳过，核树清自有分支。补水零作物进度接续不重做。
- 原补水开路归因核对确认#236已记录的共享缺陷：capture有生产时库存，完成门却仍用更早request库存，且active完成可绕过原歧义拒绝。现两门共用生产时下界/原歧义规则，仍只等需求量而非全部保证掉落；核`4b744100`和10月16日JSON原意，不引入外来堆栈估算。只目标库存变化时重观察，非逐帧full扫描；已收集回执不再假称全部掉落消失。普通拾取/捕获白名单/战斗/移动/默认不变。[合同](action-origin-required-output.md)。
- 新门先红复现；最终相关168/168通过，E原生模块构建零警告/错误。旧战斗源码守卫两处仅改为允许同参调用换行，不改战斗实现。完整同head CI/审查/发布见收口；不是新游戏证据，修复当前未发布。
- **唯一下一步：** 本修复验证发布后，继续原补水接近/控制的精确开路/拾取归因、收获归属与原生保存跨日，再显式managed有界真实集成；保留旧开路，不把资源变化伪作中性移动。其后原全局日历/预算/跨根/carrier、Provider/模型/manifest。L1`19/19、11/11`冻结，formal=false，Student新档21/21为训后；不启worker，local-data不提交。下方为历史。

## 2026-11-08 R2原生补水的零作物进度接续（当前唯一接续）

- #531经42步CI `42453157961`与同head审查接受，合入`ec08ea9f`、核树清自有分支，Core2443/Backend1357零失败/跳过；原维护编译/一次循环归属不重做。
- 当前E DLL同SHA`7f1e5b8e...`已重新反编译WateringCan：补水写满原容量且非浇水扣体力分支；原执行器补水后去作物路径失败可返回blocked但壶已满。本片显式消费此原生结果：一个已知blocked原生尝试、原身份/回执/step与空壶→原容量及完整资源核；不借seed零派发、不清未知、不改旧正前缀/执行器/默认。复用同owner claim/result/marker/journal/lookup/读回，记录refill=true、watering_prefix=false、0 tiles、batch未完成，无偏好/训练晋升。[合同](money-crop-refill-control-outcome.md)。
- 首75/75、扩展87/87通过；含原HTTP阻塞回执、丢原生/marker确认、重启摘要反例及同一序列接续下一原浇水编译。相关回归/最终CI见收口，非新游戏证据；完整资源深验只共用一次，无新增逐帧扫描。
- 相关回归751/752：唯一异常为旧种植夹具Mark阶段未改动的File.Move报UnauthorizedAccess；其后末次改名/旧失败组定向93/93通过（含24项新增），权限异常未复现，原始日志保留，不宣称首次全绿或臆定锁来源。checkpoint匹配EVD338，同head完整CI/审查另记，共享存储/执行器未改。
- **唯一下一步：** 完成本片验证发布，继续原补水接近/控制连续性（原开路/拾取需精确归因）、收获归属与原生保存跨日；不能把资源变化路线伪作中性移动或停用旧开路。接齐才显式managed有界真实集成，然后原全局日历/预算/跨根/carrier、Provider/模型/manifest。L1`19/19、11/11`、formal=false/Student训后21/21保持。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原维护编译与一次性执行归属（当前唯一接续）

- #530经42步CI `42451484629`及同head自动审查接受，合入`9da505d6`并核树清自有分支；Core2443/Backend1332零失败/跳过。原已完成维护结果的同owner接续不重做。
- 本片复用原控制builder/动作编译/执行循环与原浇水资源结果核，将原模型动作/完整source与ledger/覆盖/到期batch和精确目标固定；路线/维护共用一份原生claim/result核及Program执行adapter，旧路线API/schema/路径/默认行为保留。一次claim拥有一次原循环调用，非一次工具RPC；未知不当零、不重发。原操作边界full读回，无逐帧full扫描。同链原维护→原路线与重启均验。[合同](money-crop-owned-maintenance-execution.md)。
- 原57/57、首新22/22通过；新接受记录重启漏核prefix顺序经三反例复现，仅修本片重建先后，后3/3通过。初反例断言意外成功的owner未释放遮挡错误，已只修新测试资源处置。相关回归/最终CI见收口，非新游戏证据。
- 最终相关回归728/728零失败/跳过，含25项新增，checkpoint匹配EVD338；完整同head CI/审查/发布另记，本机不重复全量Core/Backend。
- **唯一下一步：** 完成本片验证发布，接原补水接近/控制结果、收获归属与原生保存跨日连续性；明确零credit的原生控制结果须用原证据，不借seed零派发。接齐才显式managed有界真实集成。原全局日历/预算/跨根/carrier、Provider/模型/manifest不反挂。L1`19/19、11/11`、formal=false及Student训后21/21不变。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2维护结果接同一持久所有者（当前唯一接续）

- #529最终head `cb92b62c`经42步CI `42449067897`接受，Core2443/Backend1311零失败/跳过，合入`a7d55248`且核树清自有分支；初head自动审查与后续当前桥回显修正/控制者核差分别归因。原浇水/资源结果核不重做。
- 本片以原sealed维护scope接同一owner的锁、序列、pending、marker/journal/lookup和完整读回；与路线共用唯一事务/恢复体，旧路线API/schema/路径不变，维护独立kind与原生前缀计数/tiles，重启重算而不信summary。通用非seed守卫覆盖维护，错API不触发callback。首42/42、扩展56/56、同链维护→原路线→重启通过；相关回归703/703零失败/跳过。末次类型分派/共用查询整理后的定向及最终CI见收口。[合同](money-crop-managed-maintenance.md)。非新游戏证据。
- 末次类型分派/共用查询整理后定向57/57零失败/跳过，checkpoint仍匹配EVD338；最终同head CI/审查另记，不在本机重复全量Core/Backend。
- **唯一下一步：** 完成本片验证发布，接原维护编译和一次性原生调用到上述共用所有者机制，不复制路线协议；继续原补水接近/控制恢复、收获归属与原生保存跨日连续性，接齐才显式managed有界真实集成。原全局日历/预算/跨根/carrier、Provider/模型/manifest保持后续层级。L1`19/19、11/11`、formal=false及Student训后21/21不变。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原维护结果消费与共享边界（当前唯一接续）

- 本片最终本地验证：相关669/669、末次定向94/94，零失败/跳过；末次核原地图与原工具元数据，checkpoint匹配EVD338。完整CI/审查/发布见收口，不在本机重复全量Core/Backend。
- 初head `4ef2de54`经42步CI `42447875583`与同head审查通过；合并前按当前桥字段继续核对，补新消费层对宠物碗被动水量/体力/状态回显的精确归因，不忽略碗本体/友情/邮件/未知字段，也不改共享投影或重采Pet。新增12例及浇水38/38、最终原路线/前缀/浇水106/106通过；最终head CI另记。

- #528最终head `a16ac514`经CI `42444890192`接受合入`386891ff`，Core2443/Backend1273零失败/跳过；初head审查两项均先复现、仅修新入口，最终控制者核差，测试/main树一致并清自有分支。原路线发起归属不重做。
- 本片复用原batch编译、原生逐步身份/回执前缀、原浇水生长核和原完整资源投影，新增原维护结果消费；已验前缀与blocked补水分别计账，不把补水算浇完。只核真实目标的`watered/needs_watering`、原水量与原生float体力，其他作物/资源/未知字段不放行。原路线源/新鲜CAS守卫/完整marker回放提取共用，旧种植期限/默认行为/整日覆盖不改。[合同](money-crop-watering-maintenance.md)。首7及共享167通过；派生字段修正后相关回归/最终CI见收口，非新游戏证据。
- **唯一下一步：** 完成本片验证发布，将维护结果及原编译/一次性原生调用接同一个owner与原journal/lookup/完整读回，不复制路线协议；继续原补水接近/恢复、收获归属与原生保存跨日连续性，接齐才显式managed有界真实集成。随后原联合日历/预算/跨根/carrier、Provider/模型和manifest/preflight。L1`19/19、11/11`、formal=false及Student训后21/21保持。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原路线发起与一次性执行归属（当前唯一接续）

- 本片最终本地验证：相关638/638、末次定向57/57，零失败/跳过；末次含目标/预算与准入后opaque ID篡改拒绝，checkpoint匹配EVD338。完整CI/审查/发布见收口，不在本机重复全量Core/Backend。
- 初head自动审查两项已核实并先复现：新中性入口漏`building_door`、新执行配置漏显式完整回执。只补本片allowlist/前置配置门；原普通建筑进门与编译前/准入后关闭回执的三反例已转绿，末次原路线/新路线60/60通过。完整CI以最终head为准。

- #527经42步CI `42439814108`及同head审查接受，合入`ef6c74a9`、核树清自有分支；Core2443/Backend1248零失败/跳过。原完成路线的同owner重绑不重做。
- 本片复用原编译Core和原执行循环：同owner gate内编译/准入，预定原生输出路径、原完整before/ledger/活动覆盖和精确队列/意图固定，原生调用前独立durable claim（未知非零），完整原回执才保留结果并走原marker/recovery。原编译器一连接器一重规划不改，中间到达与最终目标分别记录，已验同链两段与重启；原默认route profile不改，仅新入口在原动作边界取真实full后态，不拼字段/不逐帧捕获。采购/取种导航和未计账carrier保留原专属scope，不套零资源路线。[合同](money-crop-owned-route-execution.md)。首原35保持，新夹具元数据补齐后2/2、原执行循环HTTP及扩展53/53、两段扩展19/19通过；最终反例/相关回归/CI见收口。
- **唯一下一步：** 完成本片验证发布，再接原维护（含补水/恢复）与原生保存跨日连续性，接齐才显式managed激活和有界真实原生集成。随后原联合日历/聚合预算/跨根及carrier资源归属、Provider/模型与manifest/preflight，不把全局证明反挂单根。L1`19/19、11/11`、formal=false及Student训后新档21/21保持。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原路线证据接回同一owner（当前唯一接续）

- 相关回归618/618、末次定向35/35通过、零失败/跳过；末次含原控制观察八例复用、新route启动记录排除seed协议及更精确反例断言，完整CI见收口。checkpoint仍匹配EVD338，不在本机重跑全量Core/Backend。

- #526经42步CI `42436793211`及同head审查接受，合入`5e16e081`、核树清自有分支；Core2443/Backend1213零失败/跳过。原不完整准备追加续编不重做。
- 本片新消费者复用原生命周期路线/身份/回执和完整资源不变核，再由原零claim marker服务及完整回放更新来源；已完成原路线作为同一owner的独立phase，原run/executor/完整plan覆盖/接受源及intent固定、一次性marker调用、原journal/lookup/完整Get恢复。不是另写执行器、不是将行走等同idle，也不把路线当整周期或训练标签；旧活动crop binder/种子期限/执行器保持。[合同](money-crop-supporting-route-continuity.md)。首叶4/4、同owner首9/9及扩展34/34通过，相关回归/完整CI见收口。
- **唯一下一步：** 接原路线发起/编译/派发与此持久接续边界，再原维护/原生保存跨日连续性；接齐才显式managed激活与有界真实原生样本。其后原联合日历/聚合预算/跨根、Provider/模型和manifest/preflight，不反挂成局部整场游戏验收。L1`19/19、11/11`与formal=false保持，Student新档21/21在训练后。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原不完整准备的追加续编（当前唯一接续）

- 本片最终相关495/495通过，零失败/跳过，含26项新增；末次仅修新夹具方法提取的缩进，checkpoint仍匹配EVD338。完整CI/发布见收口，本机不重复全量Core/Backend。

- #525最终head `5c302f9e`经42步CI `42433919421`及同head审查接受，Core2443/Backend1187零失败/跳过，合入`47648545`、核树清自有分支。原严格零派发收口不重做。
- 同一owner为尚未派发的原不完整准备钉续编命名空间，只凭原intent/完整base/marker/journal/lookup及当前完整marker读回调用原编译器；旧失败不覆盖，缺/未知不重交mutation。已有队列原校验不兜底重编，纯编译无队列失败可追加独立attempt。完成视图显式path/SHA和transitive hashes接原私有准备证书/owner pin，原完整native恢复、准备态恢复和零派发消费者共用，旧默认读取不改。[合同](money-crop-partial-preparation-continuation.md)。
- 初采购2/2、扩展9中8/9仅新测试复用恢复输出目录已修，采购17/17通过；三类首21/22暴露新种植adapter重复绑定，仅新adapter改为复用队列才绑定、重新编译交原callback，保留旧一次性限制，后22/22通过。最终扩展/相关/CI见收口，不将fixture冒充游戏/训练证据。
- **唯一下一步：** 发布本片后，接原行走/维护/原生保存连续性，再显式managed激活与有界原生样本；然后原联合日历/聚合预算/跨根、Provider/模型和manifest/preflight。旧活动作物binder/idle界限不改，不反采L1。`19/19、11/11`及formal=false保持，Student新档21/21在训练后。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原控制器零派发的持久收口（当前唯一接续）

- 本片相关469/469、共用守卫提取后最终75/75通过，零失败/跳过，含21项新增；checkpoint仍匹配EVD338。完整CI/发布见收口，本机不重复全量Core/Backend。

- #524最终head `f9d8389a`经42步CI `42431798046`及同head审查接受，Core2443/Backend1166零失败/跳过，合入`e333a279`、核树清自有分支。原完整准备态恢复/一次性循环归属不重做。
- 核`c53115f0`及JSON原上下文，原严格零派发身份/源路径/queue/完整before-after/tick/停止/marker检查提取共用，不改旧默认；文件守卫原样提为runtime/lifecycle/recovery共用，避免运行事务反向依赖恢复器。只新owned runtime在原严格检查后留完整停止上下文；同一owner live或重启全验收口，类型明确非native、零请求、无proof/训练标签，旧claim不重试。日志success不当证明、缺原上下文仍待定。[合同](money-crop-controller-stop-recovery.md)。
- 初新旧11/11、新重点19/19通过，新增持久上下文确认丢失/从未落盘反例及共用守卫移动后的验证/最终CI见收口。不以协议fixture冒充新游戏或Teacher证据。
- **唯一下一步：** 完成本片发布，接真正不完整准备/缺完整queue的原intent/journal/lookup/编译器恢复，追加保留而不覆盖旧失败，未知mutation不重发；再原行走维护/保存连续性及显式managed有界原生，再原联合日历/聚合预算/跨根、Provider/模型与manifest/preflight。L1`19/19、11/11`和formal=false不变，Student新档21/21在训练后。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原准备态恢复与执行循环归属（当前唯一接续）

- 本片最终相关448/448通过、零失败/跳过，含20项新增；checkpoint仍匹配EVD338。完整CI/发布见本片收口，本机不重复全量Core/Backend。

- #523最终head `1f5f6e6d`经42步CI `42429129597`及同head审查接受，Core2443/Backend1146零失败/跳过，合入`834c4cca`、核树清自有分支。原intent预固定/准备归属不重做。
- 新owned seed-start v2在原执行循环前持久一次性dispatch claim；它表示进入循环、不是native请求证明，`native_request_performed=null`不能改成零。只有尚未进入循环的新协议准备态可复用原完整准备/marker/queue/账本检查恢复同一个原runtime；已声明未知和旧记录不重发。复用原种植八格prefix、原事务和执行循环，旧standalone默认/共享执行器不改。[合同](money-crop-prepared-runtime-restore.md)。
- 原owner初36/36、新重点首12/15后15/15；两项新测试字段/文件名错误仅修夹具，另一项确证执行后marker lookup可能退休，现只在全验后留存查回的原回包，已知坏回包仍拒绝。相关/最终CI见收口；不将协议样本冒称游戏实测。
- **唯一下一步：** 完成本片验证发布，再复用原严格`CompleteWithoutNativeDispatchAsync`处理已知控制器零派发与未完成准备/缺完整queue，未知不清不重发；接原行走维护/保存连续性及managed有界原生，再原联合日历/聚合预算/跨根、Provider/模型与manifest/preflight。不重采L1，`19/19、11/11`与formal=false保持，Student新档21/21在训练后。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原seed准备调用归属与预固定intent（当前唯一接续）

- 本片最终相关428/428通过、零失败/跳过，含10项新增场景；checkpoint仍匹配EVD338。完整CI/发布见本片收口，本机不重复全量Core/Backend。

- #522最终head `6d6482f8`经42步CI `42426869336`及同head审查接受，Core2443/Backend1136零失败/跳过，合入`4f057050`且核树清自有分支。同一owner的生命周期接线不重做。
- 本片准入用原binder/preflight/intent及准备细节固定原SHA，原seed准备factory与生命周期共用一次性调用记录/锁/pending；只在实际sealed runtime私有证书、原身份/batch/claim及真实marker journal根验证、owner准备记录持久后返回原runtime。旧standalone Prepare+Pin保持，不追认旧记录预固定。完整原准备/原生proof/消费托管/journal/最终读回已由原consumer验证后，才可补回缺失的owner准备记录并接受；不是重放或伪零执行。[合同](money-crop-seed-preparation-ownership.md)。
- 初45/45、扩展49/49通过，新增测试不可达分支警告已移除；转运/种植各增原factory抽样，最终相关/CI见收口。未启动游戏或新native来源，不晋升L1/训练。
- **唯一下一步：** 本片发布后继续准备-only/部分准备恢复与native派发归属，复用原journal/lookup和`CompleteWithoutNativeDispatchAsync`严格控制器停止上下文；缺proof不当零执行，未知不清不重发。再原行走维护/保存连续性、显式managed有界原生样本，然后原联合日历/聚合预算/跨根、Provider/模型与manifest/preflight。旧活动作物binder/idle界限不改，L1`19/19、11/11`、formal=false与Student训后21/21保持。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2同一owner的生命周期准入/接受（当前唯一接续）

- 本片最终相关418/418、末次接口整理后owner41/41通过，均零失败/跳过，含15项新增生命周期owner场景；checkpoint仍匹配EVD338。完整CI/发布见本片收口，本地不重复全量Core/Backend。

- #521最终head `c2d18d85`经42步CI `42424409057`及同head审查接受，Core2443/Backend1121零失败/跳过，合入`1ec5bfa9`、核树且清自有分支。原生命周期恢复consumer不重做。
- 本片将生命周期接同一个OS租约/锁、序列、pending和接受/恢复头；准入钉原plan覆盖/原完整账本/已接受源的相同内容及原intent SHA，原生seed旧记录保持兼容。首次原runtime调用前持久started claim，未开始可恢复首次调用、已声明但结果未知只原恢复，不因目录缺失重发。正常路径直接调用同一原RunAsync、复用已验结果和实际journal根，不新增backend Get、执行器或业务proof；生命周期接受记录显式非native。[合同](money-crop-owner-lifecycle.md)。
- 首3/3及混合owner37/37通过，含真实协议的原采购→生命周期→重启末头；claim扩展首38/39为新测试把缺目录误限定为缺文件，只改该断言，原错误帧不改。最终定向/相关回归/CI见本片收口。未到期日历拒绝；既有日历/无资源原runtime和恢复证据不重采，新的正常日历owner样本随原生连续性接齐后验证，不伪造日期跃迁。
- **唯一下一步：** 本片发布后继续同一owner的原seed未pin/部分准备、缺proof、零与未知派发及尚未派发变更归属，再原行走维护/原生保存连续性；保持旧活动作物binder/idle边界，不清未知、不重发。接齐才显式managed激活/有界原生样本，然后原联合日历/聚合预算/跨根连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`与formal=false保持，Student新档21/21在训练后。无游戏/服务器/API worker，local-data不提交。下方为历史。

## 2026-11-08 R2原生命周期恢复边界（当前唯一接续）

- 本片最终相关403/403通过、零失败/跳过，含19项新增恢复场景及旧生命周期/采购/转运/种植/owner；checkpoint仍匹配EVD338。完整CI/发布见本片收口，不在本机重复全量Core/Backend。

- #520最终head `de6272f4`经42步CI `42423375907`接受，Core2443/Backend1102零失败/跳过；初始自动审查backend遗漏已复现修复，最终小差控制者审查/26专项与CI独立归因。合入`7602281a`，核树一致并清自有分支；唯一seed所有者不重做。
- 本片为同一所有者接原生命周期恢复consumer：原intent SHA/run/mode/reason/input/source/release身份，原取消完整回包或取消前已留的完整closure账本、原日历协调及原释放kernel。释放后Get不能当取消账本；释放回包缺失只原journal/lookup且先于最终Get，已知坏回包/缺journal/未知lookup不兜底不重发。共享原seed恢复文件守卫/portfolio读取器，原runtime只抽同一元数据构造/参数检查，不改执行器或旧完成语义。[合同](money-crop-lifecycle-recovery.md)。首期新旧生命周期49/49通过，新增缺失/坏读回反例及相关回归/最终CI见收口；非新游戏/训练证据。
- **唯一下一步：** 发布本片后，将生命周期准入/接受及此恢复结果接回原唯一phase所有者，同时继续未pin/部分准备、缺proof/零与未知派发、尚未派发变更的原所有者处理；未知执行不清不重发。不另开规划器/执行器；再原行走维护/保存连续性，接齐才显式managed激活/有界原生样本，然后原联合日历/预算/跨根连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`、formal=false保持，Student新档21/21在训练后。无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2唯一持久seed阶段所有者（当前唯一接续）

- #519最终head `d516a287`经42步CI `42417971193`及同head审查接受，合入`94223ee1`，Core2443/Backend1076零失败/跳过，核树且清自有分支。原事务恢复消费者不重做。
- 本片接同一原完整plan/request/input/run/executor及初始源/完整账本，OS排他租约加实例串行准入，持久单个pending及追加式接受；实际原sealed runtime私有证书核scope/marker/原batch/claim，正常接受复用已验上下文、不增加后端读或原生proof重建。重启先恢复pending；无pending的已接受head仍须原恢复核完整backend边界。写入不确定则本实例拒绝继续，未知原执行不清不重发。原执行器/旧恢复/种植上限和默认行为不改。[合同与明确遗留](money-crop-managed-phase-owner.md)。
- 首17项中16通过，新增种植夹具把真实planner步骤混用硬编码batch/claim，已仅修新adapter原样传步骤。PR审查指出新证书漏核backend，已先复现错误接受、再只补新证书/所有者的精确规范origin绑定，保证原intent/journal重启身份，不改旧runtime默认。最终owner26/26零跳过，含选槽/部分消费、回包/接受确认丢失、可变返回DTO、排他/并发、坏索引/目录冲突及异后端拒绝。收尾新增反例前相关380/380通过；旧恢复9/9在显式E原生依赖还原后通过（首次no-restore残留治理profile缺游戏引用，不改生产代码绕过）。最终CI见本片收口；合成原生格式不冒充新游戏证据。
- **唯一下一步：** 本片发布后继续同一所有者的未pin/部分准备、缺native proof/零派发与未知执行区分、未派发业务变更及生命周期跨进程恢复；复用原journal/lookup和取消/日历/释放路径。再接原行走维护/原生保存连续性，保持活动作物binder拒绝和idle边界，接齐才显式managed激活/有界原生样本。随后原联合日历/聚合预算/跨根连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`和formal=false保持，Student新档21/21在训练后。无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2原事务恢复边界（当前唯一接续）

- #518经同head审查/42步CI `42415768717`接受合入`cdaa347e`，Core2443/Backend1062零失败/跳过且核树清分支。唯一scope选择已接受，不重做。
- 本片为唯一持久phase所有者提供原事务重建消费者：钉住原intent SHA/run/executor/输入，复用原intent构建、scope/marker/settlement/ResumeTransition与原journal读取合同；核原完整准备/base/marker/queue、原生proof、实际消费/种子托管与最终完整读回。不凭summary成功标志推进；缺回包仅原lookup且在最终Get前，已知坏回包/缺journal/缺proof/lookup不存在均不重发或伪作零执行。原Get仍可按旧规则协调，不冒称全局无副作用；共享写入/异常帧/旧执行器规则不改。[合同](money-crop-managed-runtime-recovery.md)。
- 首9/11暴露新consumer错误限制before必须同路径，已依原完整内容/哈希合同修新限制，改变before字节仍拒绝；随后12/12、最终相关358/358通过，含新增恢复14项、零跳过，完整CI见收口。合成原生格式非游戏实测；returned boundary保留原身份并在读取时再核原transitive artifacts，不证明重启游戏未回滚。**唯一下一步：** 本片发布后以此接唯一持久phase所有者的run/plan/request/input、原before、排他pending准入和接受推进；部分/未留proof/尚未派发变更、生命周期跨进程恢复仍须原所有者路径，不冒称本reader已全部解决。保持活动作物binder拒绝/idle边界，行走维护跨日靠原证据，不假改源。接齐才显式managed激活/小样本原生，再原日历/预算/连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`、formal=false及Student训后21/21不变，无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2统一自有种子scope选择（当前唯一接续）

- #517最终head `c55072dd`经42步CI `42414916363`接受合入`6c83d3d9`，Core2443/Backend1044零失败/跳过且核树清分支。首head审查指出新origin回调问题已复现修复，最终34/34/控制者审查与最终CI独立归因；生命周期持久运行接线不重做。
- 新唯一选择边界将原step绑定到原采购/转运/种植scope，冻住嵌套claim/intent/tiles并在控制前核物理输入；采购复用原缓存intent/数量上限，转运核原箱claim完整投影，种植只用已结算当前源。新控制入口只转发至同一原Core，不重写计划/执行器/恢复器、不重复种植昂贵预检。自动入口仍关闭。[合同](money-crop-managed-seed-scopes.md)。
- 最终20/20零跳过，含原采购三次丢回包lookup、原转运控制/结算、原8格种植循环/结算及旧入口；非新游戏验收，完整CI/发布见收口。**唯一下一步：** 本片发布后完成唯一managed阶段所有者的持久run/plan/input身份、原before留存、单个待定操作准入及部分/崩溃恢复，串采购/转运/种植/生命周期。旧source binder拒绝活动作物是设计，不得放宽；行走/维护/跨日须原证据/结算推进，idle重捕获不是旅行证明，未知旧执行不清不重发。接齐才显式版本入口激活/小样本原生验收，再原联合日历/预算/连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`、formal=false和Student训后新档21/21保持。无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2生命周期持久运行接线（当前唯一接续）

- #516最终head经42步CI `42412481709`接受合入`c53115f0`，Core2443/Backend1010零失败/跳过且核树清分支；首生产head自动审查与最终源守卫小差控制者审查分开归因。种植原公共事务/8格批次/新鲜前缀/结算不重做。
- 本片以显式`close-crop-seed-reservations`接已验生命周期核心、原HTTP/持久writer/portfolio journal与lookup。取消前不先Get，丢回包只核原journal与唯一原历史完整重放、不重发；日历完成用原Get及完整协调回放，仍生长不释放。只释放本作物种子/现金，无资源不发空mutation；释放丢回包先lookup、禁止Get兜底，其他root全核。create-new intent独占输出，源在首次协调前复核，最终完整持久读回；不新写执行器/业务完成规则。[合同](money-crop-seed-lifecycle-runtime.md)。
- 相关生命周期/种植/转运/采购回归240/240零跳过；读前源守卫后32/32通过。PR审查指出新回调未共用origin规范化，末尾斜杠两模式均真实复现404；仅新wrapper分离portfolio配置/保留原目录和异步异常边界，最终runtime34/34零跳过，含丢释放回包lookup。最终CI见收口，失败日志保留。合成协议不是新游戏证据，重复输出只拒绝、跨进程恢复不冒称完成。**唯一下一步：** 验证发布本片，再统一购买/转运/种植/生命周期的managed scope/claim选择、原before留存、部分/待定执行与崩溃恢复所有者；接齐后显式版本入口激活与小样本原生验收。之后原联合日历/聚合预算/连续性、Provider/模型与manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后。无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2自有种植运行前缀（当前唯一接续）

- #515最终head经42步CI `42407270534`接受合入`6b076e22`，Core2443/Backend978零失败/跳过，同head审查无发现并核树清分支；生命周期释放核心不重做。
- 本片种植复用原公共runtime事务、原marker/settlement journal与lookup、完整base/marker/final读回和唯一执行循环。保持最多8格原批次，原权威账本与实际新鲜源明确分离，逐次核已验前缀才派下一项；不放松旧源门、不假初源、不拆逐粒队列。原生结果在旧提前停止分支之前核验，未验效果保留原journal/after并停止；零次派发单独诊断，不伪造失败动作或成功。原桥有效生长参数在mutation前排除不支持profile；无额外全量快照，复用读器并减少重复深验。[合同](money-crop-owned-planting-runtime.md)。
- 新前缀/runtime最终32/32零跳过，含实际control/HTTP摄取/原循环/结算；先前相关回归292/293、种植103/104的失败均保留，新消费端预期序列和夹具重建/编码问题已修，不松旧门；完整最终CI见收口。合成原生格式不是新游戏实测，自动handoff仍不造scope，原执行器/动画保持。**唯一下一步：** 完成本片验证发布，再将已验完成/取消核心接原持久runtime所有者与journal/读回；统一购买/转运/种植的scope和claim选择、原before留存、部分执行与进程恢复，接齐后显式激活managed入口和小样本原生验收。之后原联合日历/预算/连续性、Provider/模型及manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后。无游戏/服务器/worker/API；赠额检查因未登录未确认，合作未恢复；local-data不提交。下方为历史。

## 2026-11-08 R2自有种子生命周期释放（当前唯一接续）

- #514最终head经42步CI `42404047790`接受合入`a3869f97`，Core2442/Backend947零失败/跳过并核树清分支；自动审查在首head无发现，最终时钟小差由控制者审计/专项，不冒称最终head自动审查。原生格式种植消费结算不重做。
- 本片取消/到期均接原服务：显式原因的原取消回包完整重放后才释放本作物种子/现金；到期读回共用原仓库的作物→搬机器→机器支持协调顺序，旧规则/锁/保存逻辑不改。买齐时释放采购余款已经存在，不重复写；无活动资源不发空mutation。完整portfolio回包/持久读回均核全账本，其他root不误释放，pending转运不清。[合同](money-crop-seed-lifecycle-release.md)。
- 取消丢回包只凭唯一原历史事件/原取消与读协调精确回放恢复，不重发；portfolio仍原journal/lookup且先lookup后协调读回。日历到期不是原生收获成功，不产生Teacher/训练标签。新增Backend31/Core1项：Core首轮2442旧项通过，新断言操作名误写修正后账本组11/11；Backend首轮976中974通过，旧Museum超时和新夹具混用重启前后仓库缓存的失败保留，均不改旧规则；最终生命周期31加旧组14共45/45通过。最终CI/审查/核树见收口；合成协议非新原生游戏证据。
- **唯一下一步：** 完成本片验证发布，再把种植及生命周期接唯一共用runtime事务/托管所有者、原intent/journal/lookup及完整读回，已验前缀核新鲜源再派下一项；不松旧源门、不用假初源、不拆逐粒队列。上游排除不支持的生长加速条件，原生小样本前做有界验证优化；接齐才显式启用managed入口。之后原联合日历/预算/连续性、Provider/模型及manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后；无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2自有种植原生消耗结算（当前唯一接续）

- #513经42步CI `42400639598`、同head审查及核树接受，合入`4a2377b3`；Core2442/Backend909零失败/跳过，自有分支已清。精确槽编译/原生入口不重做。
- 本片接同批次逐步原生格式回执和after：完整成功沿原回执校验器，部分前缀保留实际计数/未完成/阻塞语义；每粒核原槽扣一与原逐格作物初态，失败后缀须无资源/持久变化。原转运边界改为共用名称并复用，不复制执行器；旧整日合同不改。[结果/结算合同](money-crop-owned-planting-settlement.md)。
- 原supporting-transition只扣已验前缀，耗尽仅完成该材料claim、不取消仍生长的作物；完整回包独立重放、原文件库及供给核对。proof只create-new/flush，绑定原输入/完整marker账本，恢复核预期SHA/原生重建不重发。首37新增项在完整Backend946中通过，唯一旧Museum坏回包恢复16秒超时保留，该旧组14/14通过；随后首次源时钟一致性补第38项，种植75/75通过、零跳过，不改旧超时/转运门。最终全量CI见收口；合成夹具非新原生来源。
- **唯一下一步：** 完成本片验证发布，再原API完成/取消释放；随后将种植接唯一共用runtime事务/journal/读回与已验前缀的新鲜源派发，不松旧源门、不假用初源，也不拆成逐粒队列过测。全部接齐才显式启用managed入口和小样本原生验收，再原联合日历/预算/连续性、Provider/模型及manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后；无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2精确自有槽种植绑定（当前唯一接续）

- #512经42步CI `42395289703`、同head审查/核树接受，合入`077e237b`；Backend872/Core2442，自有分支已清。采购/转运共用事务不重做。
- 依原源/供给/marker回放绑定原种植批次到一个自有玩家槽，保留原最多8格及claim数量边界；前置排除错布局/种子/owner/位置。原编译/派发只新增显式绑定分支，绑定全部参数和顺序，拒绝剥字段降级/重复/额外命令。新参数沿原执行请求进入原种植/扣种函数，不新增执行器。仅新flag启用精确槽/品质/逐步栈数核对；旧首匹配及旧wire保持。
- 已查Git `ac84194d1`→`44c98f4bb`及有界历史JSON；没有凭后见之明判旧逻辑错误。已从实际E DLL重新反编译HoeDirt类型，确认原种植创建与扣种分工；原生构建零警告/错误、不部署。按动作只读所选JSON槽，完整供给原scope验一次，文件守卫不在逐粒循环内。专项/全量/CI见收口；合成协议不是新原生来源。[合同与明确遗留](money-crop-owned-planting-binding.md)。
- 本地完整Backend907/Core2442先通过，随后实际HTTP/大小写反例的最终专项37/37通过、零跳过；真实文件库回包及原请求映射均使用原接口，HTTP未触发内层Teacher。最终E构建零警告/错误；完整hermetic仅交同head CI一次、不在本机重复。最终全量CI/审查/核树见收口，早期夹具日期/字段名失败保留。
- **唯一下一步：** 同一批次的逐步原生回执/after、精确消耗及部分成功前缀、完整资源边界与原supporting-transition结算，然后完成/取消释放；再唯一managed激活/恢复及原生小样本。真实中途源会变化、账本仍绑定批次初源，需在新consumer按已验前缀接原派发，不改旧源门、不为过测拆成逐粒队列。之后联合日历/预算/连续性、Provider/模型与manifest/preflight。当前仅精确槽绑定，未全托管结算/开训；L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后。无游戏/worker/API/服务器，local-data不提交。下方为历史。

## 2026-11-08 R2自有种子共享运行编排（当前唯一接续）

- #511经42步CI `42391118629`、同head审查及核树接受，合入`e577b2ec`；Backend849/Core2442，自有分支已清。转运原生格式结算核心已接受，不重采L1/E。
- 当前将转运可选scope接原control/compiler/native队列及原journal/lookup，采购事务抽为唯一共用运行主体；旧采购公共API/文件名/schema/默认语义保留，完整base/prepared/final读回及各阶段回包独立回放不减少。转运不走新购种子commit，不另写执行器/恢复器；实际编译等真实marker读回，候选仍执行前锁定。
- 新提交前预检复用原通用转移候选的站位/时间与原owned primitive的完整准备投影编译，各自身份不混用，不反改旧绑定。原中断优先级保持，日期/种子/claim/intent/数量/位置/源路径须同原step。合成夹具补齐路线所需字段/来源/tick，不放宽旧校验器。[合同与接续](money-crop-seed-shared-runtime.md)。本片测试/CI以最终收口为准，失败迭代保留；不是新增原生游戏证据。
- 本地完整Backend872/872（新增23项）、Core2442/2442及原完整hermetic通过，零失败/跳过；早期新consumer绑定/夹具元数据、typed队列重序列化和年龄阈值误设的失败均保留，旧校验/超时/执行器不放宽。最终同head CI/审查/核树见PR收口。
- **唯一下一步：** 完成本片发布，再把已有种植端接精确自有槽消费及完成/取消释放，先查旧pending-transfer恢复接口，不清未验或部分已执行准备。随后唯一managed激活/恢复、显式入口原生小样本，再联合日历/预算/连续性、Provider/模型及manifest/preflight。当前自动handoff不造scope、活动claims保护仍在；L1`19/19、11/11`和formal=false冻结，Student新档21/21在训练后。无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-08 R2转运原生凭据与原结算核心（当前唯一接续）

- #510经42步CI `42386221700`、同head审查及核树接受，合入`088b932b`；Backend805/Core2442，自有分支已清。准备/派发已有，不重采L1/E。
- 已将同一准备接原完整队列回执、原before/after槽位投影和原supporting-transition；所有目的格及其他owner被动压缩均保留。完整结果按实际时间独立回放、再检查新供给。原执行器/旧机器及Museum合同/共享持久边界不改；既有玩家槽校验只改internal可见性复用，不改行为。
- 依原发布器核完整raw材料图、四桶数量/槽数、箱占用/搬动状态、原生容量/前后种子回显和`farm.chests`仓储镜像；只把已核派生值投影到内存比较视图，未知进度不丢、真实快照/hash不改。旧物品元数据守恒，新物品共同字段核来源，其他新字段观察留存、不凭空反推。原生一步after文件单独出现也绑定。proof只create-new/flush；恢复核预期SHA/原生重建且只读。合并同路径物理守卫，避免一次边界重复读大源。[合同](money-crop-seed-native-settlement.md)。
- 原生格式协议检查新增44项；本地全量初次847项中新增42通过，1个旧博物馆坏回包恢复15秒超时，失败保留，该旧组独立14/14通过，不改旧超时/执行器；最终专项和CI见收口。路径身份按实际Windows/Linux大小写规则绑定，Linux不能把大小写不同的坏step文件当成同一个after跳过。未运行游戏或生成新真实native来源，不提前晋升计数/训练。
- **唯一下一步：** 原control/compiler/native queue + 原journal/lookup的转运运行编排，必须复用采购公共事务机制，完整base/prepared/final读回、执行前锁candidate；然后精确自有槽种植消费和完成/取消释放（先查旧恢复接口，不清未验或部分已执行准备）。全部接齐才唯一managed激活/恢复和显式版本入口原生小样本，再联合日历/聚合预算/跨根连续性、Provider/模型及manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后，无worker/API/服务器，local-data不提交。下方为历史。

## 2026-11-07 R2自有种子转运准备（当前唯一接续）

- #509经42步CI `42381801869`、同head审查及核树接受，合入`8e540c72`；Backend768/Core2442，自有分支已清。采购往返保持，不重采已验L1/E。
- 当前复用原材料intent/projector/portfolio准备和原编译/派发；共享源/重捕获校验只提取不放宽。箱中本承诺claim可在同一原子准备中按真实背包空位等量拆分，保留原ID/owner/用途，避免临时超额预留；其他root的余量不挪用，原投影保留箱槽压缩映射。真实准备回包必须完整回放，返回不可变marker；原持久reply lookup可在重启后恢复，非原生成功凭据。
- 新`crop_seed_transfer_*`绑定只接原派发服务的新消费分支，旧机器/Museum路径和执行器不改、不混名。原完整JSON/投影门仍在；丢控制字段、owner/数量漂移或残留crop字段降级均拒绝。新协议夹具复用既有源/地图，缺provenance时修夹具，不反改旧校验器。[完整合同](money-crop-seed-transfer-preparation.md)。
- **唯一下一步：** 接此已验证准备的原生队列回执、全部目的格/箱槽压缩和完整资源不变量、原`SettleSupportingTransition`与完整回包独立回放；再已有种植端的精确自有槽消费、完成/取消释放。不要借机器单目的格proof伪造获取图，不写第二执行器。随后唯一managed激活/恢复所有者及显式版本化入口的小样本原生验收，活动资源自动保护仍保持。最后联合日历/聚合预算/跨根连续性、Provider/模型及正式manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后；本片不是已原生转运或开训。测试/CI见收口，无游戏/服务器/worker/API，local-data不提交。下方为历史。

## 2026-11-07 R2原控制路径采购往返（当前唯一接续）

- #508经42步CI `42377567925`及同head审查接受，合入`c96f59e2`并核树/清自有分支。采购账务核心已接受，不重采L1或E。
- 原控制入口新增可选scope：默认仍同一编译体/`earn_money`，托管固定采购只给同一编译器`daily.closed_loop`，不重入全局Teacher。实际marker回包完整重放、持久读回后才编译；完整队列JSON绑定允许编码差异，原字节和实际原生队列哈希分别保留。原执行器前核队列，原生回执后、反馈/再规划前走原结算/到货commit并最终读回，三段诊断各自保留。无第二执行器/恢复器。[合同与具体接续](money-crop-seed-runtime-transaction.md)。
- 旧运行身份规范化不改：仅新适配器对同日单调、顶层/状态一致的时钟建立一次比较视图；其余状态仍全比较。旧源须有精确prior快照、前后全部claims供给检查及原marker克隆预检；真实回包才推进账本，其他owner不重标。购种后未结算的资源变化不能借重捕获洗掉，未知进度/回退拒绝。
- 真实ASP.NET/文件库/原journal测试覆盖正常、三次丢回包仅lookup不重发、完整结果/读回篡改、旧编译失败/并发及重复拒绝；本地完整Backend767通过，最终优化定向和干净CI见收口。原控制源检查直接链接到Backend，不复制/削弱，不给Core新增LiveLoop依赖。合成原生夹具非新增游戏证据。
- **唯一下一步：** 复用原prepared-transfer/箱槽压缩结算、已有种植消费端的精确所有权消费与完成/取消释放；再接scope/匹配prior源/进程恢复的唯一托管激活所有者，恢复只查原journal/重建proof、不盲重发。全部接齐后才显式版本化启用managed入口并小样本原生验收；当前自动handoff不构造scope、活动claims保护仍在。之后联合日历/聚合预算/跨根连续性，再Provider/模型及正式manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后。无游戏/服务器/worker/模型API，local-data不提交；下方为历史。

## 2026-11-07 R2逐步采购结算核心（当前唯一接续）

- #507经42步CI `42372731074`接受，合入`f85bbfcc`且测试/main树一致，自有分支已清；所有权计划输入已接受，不重做L1/E来源。
- 新增机械结算上下文：绑定原run request/当前源/完整账本/本承诺，以独立step ID经原服务建marker；原采购/续接/逐步原生持久边界验证器共用，不改旧整链短缺门或执行器。新proof只create-new并落盘flush，后续核原始artifact哈希。
- 原supporting-transition只扣实际成交价、重绑本承诺存续claims，其他root不重标；完整结果按实际时间独立重放后才验after供给。保留一次已验结算上下文，新增种子只按真实槽位delta补本目标缺额；同槽原claim原服务增量upsert，不重复预留已有种子/占用其他root。买齐用原portfolio释放余款，整包多余量保持未预留；Museum-only合同不扩用。[合同与接续](money-crop-seed-purchase-settlement.md)。
- 新28项协议检查通过，包括连续购买、共享槽/新槽、整包余量、三次mutation的其他root篡改拒绝、中性等待/真实位置切换和输入不可变/输出不覆盖；中断恢复须预期SHA及原生精确重建相符，只读留存proof、不重发。Core2442、原完整hermetic通过，留存E完整获取证明逐字节相同。最终Backend仅旧导出夹具要求新目录一次拒绝，换独立导出目标仅重跑受影响两模式加新28项，30/30通过；未改产品逻辑，失败保留，最终干净CI见收口。夹具非新原生游戏证据。
- **唯一下一步：** 将此核心接原plan/compiler/native queue及原journal/lookup API，持久回包/读回；然后原prepared-transfer/箱槽压缩、已有种植消费端的所有权消费和完成/取消释放。接齐后才显式版本化启用managed入口、小样本原生验收；当前自动dispatch活动资源保护不删除。之后仍联合日历/聚合预算/连续性，再Provider/模型与正式manifest/preflight。L1`19/19、11/11`、formal=false冻结，Student新档21/21在训练后。无worker/API/游戏/服务器/清理，local-data不提交；下方为历史。

## 2026-11-07 R2原计划的种子所有权输入（当前唯一接续）

- #506经42步CI `42369036098`和审查接受，合入`565f3a0c`，核树/清自有分支完成。运行安装入口已接受，不重写原规划回执或重采E/L1。
- 原`CropTeacherRunPlanner.Evaluate`新增可选账本输入，仍只有一个计划主体；默认旧行为/JSON保持。复用原账本校验与完整材料/现金供给校验，只按本承诺的现货预留判断买齐，别的root/同种子的另一承诺不混用，钱包不冒充专属采购预算。原13项计划回归直接链接进Backend game-free套件，不复制测试、不增加Core对LiveLoop依赖。[合同与接续](money-crop-owned-seed-supply.md)。
- 箱中现货优先发原地图位置的取货路线/精确转移intent，复用原原生转移投影检查锁/容量/堆叠，不直接移动角色或修改claim。没有待播种义务就不投影种子资源，不新增快照或全世界序列化。新字段null时不改变旧step JSON。
- 新21项及原13项计划检查通过，完整Backend703/Core2442、原完整hermetic零失败零跳过，最终Release零警告/错误；含另一同种子/同decision承诺不混用、原生数量增长不自动有所有权、预算缺失和源/槽位/品质/日期拒绝。自查外部玩家背包误归类反例先红后修，只改新consumer上游门；币种投影只序列化money/币种字段。夹具同步修正而未放宽原源/布局门；最终CI见收口。
- **唯一下一步：** 接每步独立marker commit、原候选/编译/原生回执、原supporting-transition逐件扣款/本owner重绑、真实取得槽位新增预留及prepared-transfer位置结算。旧服务有意要求step marker的SourceDecisionId=step ID；不能改初始Teacher-owned资源portfolio来迁就。其他owner不重标，箱槽压缩复用原完整投影；Museum-only托管合同不扩用为种子。接齐前自动dispatch活动资源保护仍保留，typed步骤不是已原生执行。
- 之后仍原聚合预算/联合日历/跨根与所选来源连续性、Provider/模型、正式manifest/preflight；L1`19/19、11/11`、formal=false不变，Student新档21/21在训练后。针对性检查与最终回归/CI见本片收口。无worker/API/游戏/服务器/清理，local-data不提交。下方接续为历史。

## 2026-11-07 R2作物与资源运行安装（已接受#506）

- #505最终CI `42367028701`与审查通过，合入`4405fffb`，测试/main树一致，自有分支已清。完整既有账本接入原Teacher已接受，不重复采L1或E原生周期。
- 新显式`install-crop-reservations`入口在普通Loop/训练初始化前分流，只经原crop upsert及portfolio commit安装原v2选中承诺、现货种子和精确短缺现金。按真实时间独立重放完整结果并读回持久账本；原PR504规划回执/时间戳不改，另记运行安装结果。原始请求先持久化，crop丢回包仅同源精确读回，资源丢回包仅原lookup，均不重发；部分安装失败保留证据、不自动取消或回滚。[入口与合同](money-crop-runtime-installation.md)。
- 自动dispatch暂不启用新资源安装，并在原新handoff消费端拒绝活动材料/现金claims，无预留旧行为保持。原买种候选读原生余额、不自行消费本新portfolio/建立槽位托管，旧run planner还按背包总种子数而非本计划所有权判断买齐；能编译不代表正确结算。下一步接原supporting-transition预算所有权/路线重绑/单件结算/真实种子槽位托管及箱中转移，并把本计划可用种子数接入原获取/种植消费端，接齐后显式启用managed输入。旧initializer/完整种子门保持，不改旧通用执行器适配新实验，也不把后台账本安装当原生游戏证据。
- 新20项Backend检查含原ASP.NET端点正常/双丢回包、真实仓库重载、其他root不变、三类完整结果篡改/并发写入/原请求损坏拒绝及非托管保护。完整Core2442/Backend669、原完整hermetic、原恢复脚本19通过，最终Release零警告/错误，实际CLI缺参在普通Loop前拒绝；最终CI见收口。无新原生游戏证据或分子晋升。
- 之后仍为原DailyPlanCompiler聚合预算/联合日历、跨根连续性/所选来源，再Provider/模型及正式manifest/preflight。L1`19/19、11/11`冻结，formal=false；Student新档21/21是训练后评测。回归/最终CI见本片收口；无worker/API/游戏/服务器/清理，local-data不提交。下方接续为历史。

## 2026-11-07 R2运行账本接线（已接受#505）

- #504最终42步CI通过并合入`affcc989`，已核树/清自有分支。所选种子/采购现金的原子规划重放已接受，原E6/6、875g与原失败保留，不重跑13日周期。
- 运行handoff原先未传基础账本，现一次读取原后端完整账本并以不可变路径/SHA送入原Teacher/dispatch。捐赠保留原触发门/格式，只共用原证据存取段；Core game-free不加LiveLoop依赖，提取共用测试夹具供原Core及Backend测试使用。[合同与接续](money-crop-resource-reservation.md)。
- 旧SourceHash不直接改写：先用原身份/材料/货币校验验证当前全部预留，再经原零claim marker commit推进一版，由原commit证据验证器按真实RecordedAt独立重放整个结果。现存claim不变；已匹配源不发mutation，活动作物继续原完整承诺/布局。共用原journal/lookup，丢回包只查原请求、不重发；旧settlement的操作/日志键不变。
- 20项新Backend检查、完整Core2442/Backend649、原完整hermetic及旧恢复脚本19项均通过，零跳过；含真实文件仓库提交/重载/回包查找、完整账本到原dispatch、三类结果篡改和旧settlement正常/丢回包。运行程序Release构建零警告/错误；HTTP丢包为脚本夹具，不当原生游戏证据，最终CI见收口。无新增全量快照，入口只在规划handoff触发。
- **唯一下一步：** 现有账本锚点接线不是#504新计划claims已安装。发布后接原crop-upsert及资源portfolio的真实安装/回包核对，再接supporting-transition中性路线重绑、单件采购扣款和真实种子槽位托管；不要覆盖旧initializer/源门，买齐后不重复预留，箱中现货复用原转移/relocation。然后原DailyPlanCompiler聚合预算/联合日历、连续性/所选来源、Provider/模型与正式manifest/preflight。L1`19/19、11/11`冻结，formal=false；Student新档21/21在训练后，无worker/API/游戏/服务器/清理，local-data不提交。下方旧接续仅供历史追溯。

## 2026-11-07 已核实审计问题499/500（当前唯一接续）

- #498最终CI`42354205809`全40步通过，合入`50349056`且核树一致；原控制等待已接入两个原路线证明入口，不改执行器或观察/工作计数。
- #499在原共享鱼类进度读取器补三个Number类型检查；错误类型返回原结构化拒绝，不改72分母、逐鱼身份、聚合一致性和既有进度不能倒退的规则。#500仅修语料阶段门：原睡觉仍day+1，原婚礼剧情推进可同日；全原生源独立重建、精确queue/item/nonce/option、完整婚礼及原分区归属不变。
- 新24种鱼类类型反例、婚礼双阶段/9类拒绝及旧原生绑定等定向回归共147项通过、零跳过。旧三分区婚房语料离线重建与最终CI结果以PR收口为准；合成阶段例不是新增原生来源。未开游戏、未改原凭证或制造偏好标签。
- **唯一下一步：** 完成本审计修复的旧来源重放和最终CI，回复关闭#499/#500并清自有已合分支，再接原当前v6首次原生跨日、每日维护、留存/出货结算及R2全局集成。L1`19/19、11/11`冻结，formal=false；不重采已验来源，不启动训练/worker/API/服务器，不提交原始数据或临时测试产物。

## 2026-11-07 原生跨日前的控制恢复（当前唯一接续）

- #497经CI `42351397889`全部39项接受、合入`5cc3b42c`，分支核树后清理。新D真实再次购齐5包，并完成返场、5次播种和5次浇水（两批applied/success/verified）；首次跨日前切图尚不可控，原控制层正确改发30tick等待，报告verified_actions=0/exit2，旧固定睡觉caller误作失败。原档SHA不变，D游戏/后端已退出，全部证据/失败保留，未跨日或收获。
- 核`18b59881`/`40dad9f6`及对应JSON：观察不算训练工作成功是有意合同，不改为零目标、不放宽普通exit2。抽出原Rusty Key唯一安全候选选择器（兼容包装保留原语义/错误），原作物跨日和最终出货结算复用同一有界前置：至多4次精确控制观察，按原rank/单步API取得鲜活源后才调用原睡觉。每次观察进入原路线证据链，睡觉本身的编译/断言/跨日证明不变，无第二执行器。
- 新52项、旧Rusty证据41及运行包装器50（含D真实回执/报告复核）、原I/O20和目标转发10通过。真实后端按D前后源分别编译wait与原生sleep，原文件未改；这里只证明编译/接线，不冒充已原生睡成。最终CI/发布见收口。[细节](current-production-snapshot-handoff.md)。
- 审查补齐原生命周期及终态回家路线的形态兼容：仅原`NativeControlSettleRecovery`确认的同位置30tick观察允许独立成段；普通等待仍拒绝，完整回执、日期、持久资源及终点校验保留。双原证明路径的正例及7类反例、完整hermetic通过；留存D真实等待离线同时通过原回执/生命周期/终态回家证明，原文件不变，无游戏重采。
- **唯一下一步：** 完成本片最终CI发布，先修已核实的#499鱼类聚合类型错误合同和#500婚礼语料阶段兼容，再回原当前v6完整生产入口验证首次原生跨日及后续维护/留存/出货结算；不可拼接D未保存的内存或回写旧回执，不重采L1。再R2全局能力/共享预算/日历集成。L1`19/19、11/11`冻结，formal=false，非新档Teacher21/21；无worker/API、训练、服务器或清理，原始数据/caller仍仓库外。

## 2026-11-07 共享资源边界的已知上下文（当前唯一接续）

- #496经CI `42350460137`全部39项接受、合入`416ded1f`，自有分支已核树一致后清理。没有反改Loop的applied部分进展语义。仓库外批量核对C全部9段/31原生步骤，而非每修一处重跑游戏：发现工具升级上下文、Gourmand菜单和社区中心剧情在播状态混入持久资源比较。
- 核`1f2bc568`/`167eb5f6`及对应JSON、原桥来源后，仅在原边界投影中规范已知上下文，桥/执行器/原始快照/回执均不改；工具/生命周期schema限定，未知字段保留。严格边界仍比较工具/待升级日数/容量/钱；只有原“精确资源变化”视图规范库存/货币的派生副本，仍保留升级目标/价格/材料成本等真实事实。剧情事件seen/锁定资产/邮件/完成，以及核桃请求/奖励/锁仍比较。
- 复用原C公开Build已完整接受精确短缺量：9段、4支持段、5单件采购，种子0→5、现金450→50，proof SHA`413feac7...`，formal=false。原C失败记录不覆盖、不拼接运行或重新购种；这只闭合获取证明，不是种植/留存/结算完成。共享调用者范围和验证见[接续](current-production-snapshot-handoff.md)。显式E构建零警告/错误，新增21边界场景同时检查严格/预期资源变化视图，最终hermetic/CI见收口。
- **唯一下一步：** 完成本片共享回归、精确重建和CI发布，然后使用原当前v6完整生产入口接种植→原生每日维护→产出留存→出货/日结算；保留A/B/C诊断，不以未保存内存状态接续或伪装完整生产。再既定R2全局能力/共享资源/日历组合，不重采19项L1、不训练、不用worker/API。原始数据/caller/测试临时文件均仓库外，L1`19/19、11/11`冻结，非Teacher新档21/21。

## 2026-11-07 原生采购回执兼容（当前唯一接续）

- #495经CI `42349146093`接受、合入`6e5616e0`，自有分支已核树一致并清理。独立C未再进入内层Teacher，原生同日9段实际买齐5包花椰菜，450g减至50g；离线证明拒绝第0段等待。源档SHA前后不变，C游戏/后端已退出，数据与原失败记录保留。
- 已按提交`b95c4ca8`和对应JSON上下文确认：保留已验证部分进展为`applied`且success=false是为防止丢失原生事件证据，不能改回旧blocked语义。仅作物采购消费者显式支持此形态，复用原续接逐步校验；默认整队列和旧blocked续接路径不变。要求显式progress、鲜活快照、完整状态/tick/队列/有效动作及合法续接元数据；已记录完成与活动身份必须一致。最终精确种子/费用/短缺量仍由原采购证明检查，不升级候选成功。
- 首次完整hermetic通过（含新增25检查及旧采购/续接/资源反例），显式E构建零警告/错误。原C公开Build已越过等待，继续在第4段step1拒绝`player.tool_upgrade`；实际只变化当前位置/actor-control/service-status及派生fingerprint。该共享边界尚未改，不将买齐或局部回执兼容称为全生产验收通过。
- **唯一下一步：** 完成本修复CI/发布；核工具升级原投影与持久边界的Git/JSON合同，用保留C逐步重放定位并修真实误判，保留未知/持久字段拒绝，再接当前v6生产→留存→结算及R2全局资源/日历。不重采已验L1，不改旧成功标记，不启动训练/worker/API，不提交原始数据/caller。L1`19/19、11/11`冻结，formal=false，非新档Teacher21/21。

## 2026-11-07 固定采购目标与调度归属（当前唯一接续）

- #494经39项CI接受、合入`35b83485`，CAS接线故障已真实越过。B采购阶段却继承默认`grandpa_max_score_year3`，触发内层作物Teacher并产生新承诺，故已按PID/父子/命令行核定只终止自有Loop/Backend，再由原finally退出游戏；源档和A/B数据保留。不是两套执行器，也不把诊断中止后的缺文件当新故障。
- 不改正确的全局默认值或Teacher/执行器。只给原单步helper追加可选GoalId，保持旧参数位置/默认；作物采购显式传`daily.closed_loop`，对应外层生产所有者。10项参数/AST、原20项I/O回归通过；同一真实B源后端重放证明全局handoff仍在，固定目标无handoff且精确柜台动作编译通过。[接续与证据](current-production-snapshot-handoff.md)。
- **唯一下一步：** 完成本局部修复的CI/发布，继续独立C当前v6生产→留存→结算，再既定R2全局能力/资源/日历组合。不等待或复活B，不重采19项L1、不强改旧v1标签，不训练、不用worker/API、不提交原始数据/caller。L1`19/19、11/11`冻结，非Teacher新档21/21，formal=false。

## 2026-11-07 全局生产接续：当前版本与快照存储（当前唯一接续）

- #493经38项CI接受、合入`709bfe6c`。L1并集`19/19、19/19、19/19、11/11 roots`冻结，formal=false；不是Teacher新档21/21。已核保留v22离线5/6、v10历史48转移/6/6/875g；当前重放停在有历史原因的v1/v2布局和续接终态标记差异，不是旧tick资源边界再坏。原数据和共享门不改，不造当前标签，不盲目关#338/#339。
- 原入口已取得当前9块v2农场源、真实450g，v6 Teacher在max64下自己选择5格花椰菜，无库存/价格注入。原生A首队列后暴露调用端把CAS索引当full snapshot提交造成422，错误正文又被disposed response遮蔽。现仅I/O消费端复用原校验解码器，补全复制/预留/输入/最终读取和HTTP错误正文；后端/执行器/领域门均不动。20专项、物品转移20、Core2380通过；真实后端索引拒绝/逻辑源接受/伪hash拒绝通过。[依据、范围与接续](current-production-snapshot-handoff.md)。
- **唯一下一步：** 完成本修复发布并接续B的当前v6原生生产、留存、结算，保留A失败；只修真正出现的消费端故障，不改旧共享执行规则。再R2全局能力/资源/日历组合，正式训练继续禁入。本轮不重采19项局部来源，不用worker/API，不提交原始数据/caller或训练组件。

## 2026-11-07 Money真实缺分区与有条件方法（当前唯一接续）

- #492最终CI `42341875766`接受、合入`c157d618`，自有分支已核树一致后清理。复用G/H train，只为既有隔离档补test I/J（原生day13）与validation K/L（day17）；初始运行身份与原Core分区核对后才设置测前库存/时间，原档哈希保留，四次隐藏静音运行已结束。优选实际收入`0→1,200,000`，铱锭反事实`0→20,000`，没有policy日期改写或把反事实当成功行。
- 原v5三分区聚合Build/Verify同SHA`730f36d1...`，原18格工作表确认native/comparison均18/18、无待采波、formal=false。局部方法Build/Verify同SHA`04cf816e...`，原覆盖对账`d6851f45...`使Money六项均ready，其他13项逐项不变。原图14节点、边、执行引用、阈值/7分阶梯、预留、获取/生产/结算及全局递推规则均不改；原coverage复用已验corpus，核同一图路径/摘要，拒绝删metadata降级，旧未作用域化入口保留。显式E构建零警告/错误、19专项拒绝、15类实际文件反例及完整hermetic均通过。[合同与收口](money-local-method-view.md)。最终CI接受前不晋升；接受后L1不重叠并集为`19/19 executable、19/19 split-complete、19/19 ready、11/11 roots`，formal仍false，不是完整Teacher新档21/21。
- **本PR接受后的唯一后继：** 先核原全局获取/生产、共享承诺与时间预算的真实遗留，优先对照#338/#339保留的v22/v10来源、`167eb5f6`/`858a7f8b`/`ac764c00`旧修复及已记875g结果；不是从头改边界或重跑13天。再按R2所属层接原完整Teacher21/Student集成。不因直接出售关闭crop/native条款；不重采本轮成功来源或重做局部方法。若本PR尚未接受，只处理本PR真实审查/CI失败并核发布状态。原始数据/caller仓库外，无worker/API，不改共享执行器，不启动训练。

## 2026-11-07 Money既有corpus的v5原生准入（当前唯一接续）

- #491已38步CI接受、合入`6fee622a`。本轮扩展原Money corpus为显式v5来源，不另建系统或伪造旧v2回执；默认v4和同帧门保留。核`0e4086a8`/`6f28fce1`旧意图，仅提取原目标/偏好公共检查，原native artifact/prefix验证不改；新source/join元数据旧记录省略，一组比较服务匹配criterion。[合同与接续](money-shop-replay-corpus.md)。
- 复用G/H留存源，无新游戏：实际v5正向/反向Build/Verify同各自SHA`57fd1b02...`/`2b4ac8df...`，train六阈值comparison/native合法join；旧v4验证和重新生成均保持`8147889a...`。原工作表接受v5并报告18格中6格完成、两native波次待采、12格缺validation/test，非6/19总进度。21专项反例、17类公开反例及源输出别名拒绝通过，最终hermetic/CI见收口。
- **唯一下一步：** 按原18格工作表先核原生save/player/day分区，采真正缺失的validation/test来源组，复用E隐藏静音隔离/旧执行链/显式绑定/完整比较/v5准入；不重采G/train、不改policy日期或手写分区、不把反向参考算独立样本、不把H负例当阈值成功。再条件局部Money方法/原覆盖；当前并集仍`13/19、13/19、13/19、10/11 roots`、formal=false，全新档获取/共享承诺与预算/全局collector/Teacher21/Student义务不减。原始数据/caller仓库外，无worker/API、训练、服务器或清理。

## 2026-11-07 Money完整原生重放比较（当前唯一接续）

- #490经最终38步CI接受、合入`e807e125`，真实来源绑定和G旧`1/2`保留。本轮新增显式版本化比较消费者：精确原请求分母、1个原生参考见证及其余各自实际重放结果，原`VerifyAlternativeWithContext`和原比较器共同验证；保留实际ID/前后态/nonce/费用，不伪造旧同帧回执。[合同及接续](money-shop-replay-comparison.md)。
- 一组已验来源/实际上下文服务原六阈值；仅本次调用内、完全相同来源描述可复用参考上下文，目标请求重新从原catalog选择。旧v1绑定公开重放逐字节不变，无新全局缓存或共享门放宽。真实G收入1,200,000/H铱锭收入20,000，公开Build/Verify同SHA`72e9af54...`，各2替代/1偏好；14公开反例及引用复用/异源拒绝探针通过，额外引用描述/测前设置边界已加入专项共17反例，最终验证/CI见收口。
- **唯一下一步：** 原Money criterion/corpus的显式版本扩展，保留v4默认/旧字节和原同帧路径；逐criterion验证实际被选结果与native精确join，一组比较供匹配阈值复用，不造v2回执、不将H负例当阈值成功、不按policy日期分区。再补真缺validation/test与条件方法/覆盖。本轮比较不是corpus准入，G旧index不改，当前并集`13/19、13/19、13/19、10/11 roots`、formal=false；新档获取/共享预算/全局collector/Teacher21/Student义务不减，原始数据仓库外，无新游戏、worker/API或产品训练。

## 2026-11-07 Money显式局部重放来源绑定（当前唯一接续）

- #489经38步CI接受、合入`75d1a8ab`。H真实铱锭`(O)337`出售后发生调用端同名程序集加载失败；保留原失败记录，干净依赖子进程离线复用原raw/nonce reader及原episode factory，再原终端/预留/账本/recurrence验证，实际累计收入`0→20,000`，没有重售或改写旧来源。旧报告/运行名误称Gold，真实候选明确是铱锭；执行始终按337/20件/单价1000绑定，历史路径不改。原始caller/数据仓库外。
- 新v1来源绑定仅支持声明持有库存、直接普通商店出售、revision0无承诺的隔离研究账本和单次即时出售递推。独立重建两侧完整来源，全部候选/拒绝项精确一致、完整请求双射；绑定源档/副本、测前设置/Mod记录、实际run/root、执行构建身份、账本及原持久边界。只允许分别与原生请求绑定的run/path两项持久差异，未知义务不丢弃；复用旧验证器，不改共享执行或同帧门。[详细合同](money-counterfactual-source-binding.md#implemented-local-source-binding)。最终E构建零警告/错误、29边界反例及11类公开反例通过，真实11候选/2请求重建同SHA`c9acb7f5...`；候选必须精确绑定原request的concrete→alternative→source-candidate文件路径/哈希，不能借同快照的别份合法分母。最终hermetic/CI见收口。
- **唯一下一步：** 版本化重放感知比较消费者，重验每条实际替代结果后调用原比较/准入，再corpus及缺分区。来源绑定不是比较标签；G旧同帧checkpoint仍`1/2`，不把H伪装成G片段。并集保持`13/19、13/19、13/19、10/11 roots`、formal=false；新档获取/共享预算/Teacher21/Student和原全局collector义务不减，无worker/API或产品训练。
- 非准入预检已复用原`VerifyAlternativeWithContext`和原比较器：显式映射G收入1,200,000/H收入20,000，不改前缀哈希；完整2请求在原六阈值各得1偏好。这里只确认下一消费者可复用旧逻辑，不伪造旧同帧manifest/准入或corpus，不增加覆盖数。

## 2026-11-07 Money反事实来源合同（当前唯一接续）

- #488经38步CI接受、合入`b3874405`，原G出售和`1/2`终端证明不重采。核对确认旧计划只有采集任务描述，没有可恢复的原生分叉器；完整哈希含读取tick/执行身份，不能把独立重放叫作G原帧。[来源绑定及按序接续](money-counterfactual-source-binding.md)。
- 新只读audit复用原完整request/candidate重建、active-shop绑定及原persistent projector；保留两个实际来源/候选/摘要，任何情况下都不授予重放/native/标签/训练权限。G/F真实比对经济事实、服务时间/菜单和20件Gold单价1000均相同，旧持久边界仍明确列出run ID及隔离路径两项差异；F不因审计获得Mod准入。旧验证器、缓存及执行器不改，fixture改用新不可变路径/保留原字段来源。
- **唯一下一步：** Gold own-source原生观察（不要求负例跨最低阈值，不冒充G已闭合），随后版本化来源绑定完整证明save/setup/Mod/身份/候选映射/账本资源/时间报价及全部持久事实，再原比较/准入/corpus和缺分区。原帧与实际帧保持各自身份，不改hash、不吞未知变化、不用价格预测替代原生。G仍`1/2`，并集`13/19、13/19、13/19、10/11 roots`、formal=false；原始数据/caller仓库外，无worker/API或产品训练，最终验证/CI见收口。

## 2026-11-07 Money原生出售与终端预留（当前唯一接续）

- #487经38步CI接受、合入`2f1e935b`。G局部声明库存条件下原编译出售→关菜单均执行，原v4验证累计收入`0→1,200,000`、六阈值/train、comparison=0；测前Mod身份真实保留，离线复用原raw/nonce reader及原episode factory，原passed-run/比较初始化器接受。E/F和G初始调用端失败摘要保留，不冒充获取/新档或训练。[来源与边界](money-local-native-collection.md)。
- G又真实暴露Money消费端把库存图`item.salePrice()`的6000与原生卖价3000强等价；已核原提交`b9455b337`、同期合同及1.6.15反编译，只修Money假等价，原桥、编译执行、原生精确价及预留/数量门不改。原完整hermetic通过，留存G在原收集器接受`1/2`证明、pending=1、ready=false；400可用/0预留及精确消耗/账本/recurrence通过，checkpoint `d6c614f5...`。完整CI以本轮收口为准，不重跑原生出售。
- **唯一下一步：** pending Gold Bar request `78fa1b4e...`的合法同源原生采集路径；先核原续接/重放合同，不将已售后的状态或其他run拼成同一测前来源。随后原完整index、独立比较/准入/corpus及真缺分区。G已退出、未保存内存不是活跃checkpoint。并集保持`13/19、13/19、13/19、10/11 roots`、formal=false，本轮无新增Teacher/native合法join；完整新档获取/共享预算/Teacher21/Student及全局complete-root门保留。原始数据与caller全在仓库外，无worker/API或产品训练。

## 2026-11-07 Money纯direct concrete与原请求链（当前唯一接续）

- 前序采样修复#486经38步CI接受、合入`7c78db2b`；本轮接续修concrete消费者，提交/CI以本轮收口为准。消费者按独立重建的原alternative集合处理纯direct分支：空重绑定集合复用原`BuildDirect`/身份/基数验证，不伪造回执；非空仍需原producer和完整rollout/rebind证明。原脚本共享helper只在正数时调用producer，空路径不产生伪文件，拒绝来源输出别名及被忽略的rollout；外层passed native summary门保留。[完整合同](money-threshold-production-evidence.md#current-direct-concrete-union)。
- D离线真实来源已通过原concrete/强账本/反事实请求，实际2条direct与2条待native-shop-sale证明请求，公开重复重建同SHA`2ba1857b...`/`40cce1fa...`。账本为原后端读出的revision0隔离初始条件，不冒充继承生产承诺。来源未变，无游戏或执行；最终hermetic（含原混合union）、初始化器4反例、Core2380/Backend629和5公开反例通过，最终CI见收口。
- **唯一下一步：** 先预检原反事实/原生采集的源身份、账本及recurrence合同，再经明确L1作用域的薄caller复用原执行/证明链，采same-run原生结果和独立预留比较/准入/corpus及真缺分区。原全局collector的complete-root门不删；D是离线准备，不是活跃世界或passed native run，新run重建自己的完整来源，不拼接D。18单元/R2全部长期及新档Teacher21/Student义务不减；本轮0新Money单元，并集`13/19、13/19、13/19、10/11 roots`、formal=false；无worker/API、训练、服务器或清理。

## 2026-11-07 Money经济候选采样对齐（当前唯一接续）

- #485经38步CI接受、合入`d6fa8194`。本轮仅Money单阈值/wave消费者适配原active-menu economic候选，旧event路径及共享桥/候选/编译/执行不改；原完整分母重建和累计收入/分区断言保留。原候选ID/channel/空参数/摘要不改，单独从同源实际位置/商店/商品绑定原8项执行参数；不造标签或晋升原生结果。[合同与事实](money-threshold-production-evidence.md#current-active-sale-sampling)。
- D公开direct/wave逐字节相同，SHA`db993d85...`；11条原候选/2比较，选`sell:5`，实际收入0，预测1,200,000及六阈值尚未执行。原后端实际接受参数并编译`出售→关闭菜单`；来源未变、后端退出，无游戏重跑。最终完整hermetic、旧stage和34新场景/4公开反例通过；最终CI见收口。
- **唯一下一步：** 原alternative入口已接受D的2条direct路线，但concrete消费者强求rebind回执与原producer拒绝空rebind的合同冲突已离线暴露。先查历史意图，修消费/编排层纯direct分支，不伪造空回执、不放宽真实rollout/rebind验证，再原账本/反事实请求及same-run原生结果/独立比较。D不是passed Money native run，不拼接别的run；R2原全局预检/全部长期义务保留。本轮0新Money单元，并集仍`13/19、13/19、13/19、10/11 roots`、formal=false；无worker/API、训练、服务器或清理。

## 2026-11-07 Money真实商店准备与旧恢复控制

- #484经38步CI接受、合入`48430bd1`，原入口对齐不重做。当前唯一primary仍`earn_money`；并集保持`13/19 executable、13/19 split-complete、13/19 ready、10/11 roots`，formal=false。
- E隐藏静音隔离B已通过原路线及剧情611439的原生控制走到铁匠ShopMenu，5准备队列/1剧情控制；没有出售、跳剧情或测量期资源修改，逐段库存/现金/累计收入保持，原档哈希不变、自己的进程退出。原生商店安全计时器250ms仍在；这不是收入证据。
- Money分母现窄绑定旧恢复器的真实ShopMenu关闭前置，保留候选原kind/效果/全部行并重新封签，不当作睡觉结算、不进入比较。共享桥/候选/编译执行不改，未知或伪造控制仍拒绝。保留B公开重放由9/10变为10/10节点，但比较仍0、ready=false，计时器与全局义务不放宽。[合同与接续](money-threshold-production-evidence.md#native-shop-recovery-prerequisite)。
- D测前原生30tick校准实际已完成，计时器-6ms；新caller误要求精确0，只修caller、保留原失败报告，离线资源/原档核对后接续，不重跑游戏。原公开分母接受10节点/11候选/2比较、train、收入0，SHA`0f7258ed...`；无出售或标签。该校准不是产品编译动作，D缺失的直接请求如实标为缺失，不伪造nonce或回执。最终完整hermetic、16新场景和3公开入口反例通过；最终CI见收口。
- **下一步：** 原threshold selector已在D离线重现仅认event continuation、不认旧active-menu economic候选的拒绝；直接修Money采样消费端及wave路径，绑定同源真实候选/位置，不改共享接口、不伪装route或缩减分母。复用D及原比较/native/账本预留链，六阈值三分区独立join后才晋升；原全局collector预检及R2所属长期义务保留。原始数据/临时caller仓库外，无worker、API、训练、服务器或清理。

## 2026-11-07 Money6：库存、续接与 bounded 布局

- #483 经 CI `42313560862` 全部38步接受、合入 `33cf0a4d`；其24项新检查和Core2378/Backend629通过，未重跑游戏或增加Money分子。新一轮确认两个旧PowerShell初始化器把已生成的v6决策/回执错当v1拒绝，现删除包装层的策略版本副本，仍由原C# Build/精确重建拥有策略校验；原输入reader/执行器/持久边界不改。16项无游戏集成检查贯通旧入口，执行形状与当前自动dispatch一致，伪策略/篡改数值/覆盖均拒绝；接入原CI步骤，不额外启动游戏或重跑长轨迹。[具体合同](money-threshold-production-evidence.md#initialization-alignment)。
- #482 已由 CI `42310416136` 全部38步接受并合入 `ba1de89f`，本项转到唯一 primary `earn_money`。冻结并集 `13/19 executable、13/19 split-complete、13/19 ready、10/11 roots`，formal=false；修 bug 不填 Money 的 `6×3` 生产证据单元。
- #338 当前代码复核：有限库存确实丢失；Teacher 活动账本续接会漏重述旧承诺；布局已有晚期超界拒绝，并非无保护放行。修复保留旧成功决策和原强回执，仅在 Teacher 端绑定完整准入身份并封顶供给、提前拒绝无布局续接，以及超界时选择已读合法 bounded 区域。新供给/布局 partial 拆开，不新增第二规划器或执行器。[合同与验证](money-threshold-production-evidence.md#current-money-resumption)。
- #339 的持久边界问题有后续源码 `167eb5f6` 和历史完整段/结算记录，不能照搬旧 issue 重做。原 v22、pricefix-v10 与新一轮现场必须分清；历史875g未跨最低50,000g。原始证据缺失不补造；核对保留来源，不以路径缓存或历史报告冒充当前深验。
- **下一步：** 按R2先核对Money criterion-local消费者与原全局collector的责任边界，复用既有当前分母/精确比较/原生结果/账本预留构造六阈值局部证据，不倒挂完整新档百万收入日程。原全局collector的complete-root预检保留；其新档seed-robust日程、跨根机会成本及未来生产/预留义务完整留在所属L3/L4层，局部选中的实际生产/预留仍必须证明。保留来源只按已知索引核对，不扫描全盘或重跑13日恢复历史文件。复用原作物 Teacher/购买/预留/生命周期/留存/出货/日结算，使用累计收入而非钱包；不重采已验根、不把局部条件证据说成新档Teacher21或Student训练。无 worker/API、产品训练、服务器变更或文件清理；回归及最终CI见提交收口。

## 2026-11-07 全馆三分区与原准入收口（当前唯一接续）

- #481经CI `42306628987`全部38步接受、合入`669142f2`。原代码不改，只新增E隐藏静音validation冬12/207及test秋20/187，原C#分区与原生读回日期/身份双检；同一存档冬季无test，预检正确避免照搬婚房日期。原caller/B不动、原档哈希不变、两次游戏/后端均退出。[完整来源](museum-local-acceptance.md)。
- 原强reader及corpus/条件证书/覆盖实际接受3源/3pair/3原生终态、全95项、三分区，本条件方法executable、criterion ready/本项无open work，另18项与固定同代码基线相同。corpus SHA`d86c7847...`、视图`3961a608...`、对账`cc3859cf...`；每个源slot11 `(O)96`栈2→1、tick126→331/原生遥测204。保留源均未变；准入未用路径缓存跳过当前proof，数据/caller/view/corpus/local-data全在仓库外。父代码Core2354/Backend629/38步CI不变，本轮显式E原生和原准入通过，最终CI见收口。无worker/API、产品训练、服务器或文件清理；后台来源不等于可视帧率验收。
- **唯一下一primary：`earn_money`（Money6）**。全馆最终接受后冻结并集`13/19 executable、13/19 split-complete、13/19 ready、10/11 roots`，formal=false；该数是局部criterion并集，不是同一新档已获13项或Teacher总完成。先核对现有Money代码/原生耐久来源与#338/#339等issue，再补实际缺口，复用原作物Teacher/售卖出货/机器/承诺/日结算，不另起收入规划器、不重采B或旧根。全95获取/RNG/共享资源/Year3全局预算及新档Teacher21/Student义务保留。

## 2026-11-07 全馆条件方法与独立证书（当前唯一接续）

- #480经CI `42303876747`全部38步接受、合入`2f398e31`。本局部方法按旧Marriage/Friendship模式消费已验corpus，原图不动；全部6角色/旧边/未知字段及10个其他方向保留，完整旧方向、阻塞与递推复制为显式全局责任。不将已有94件/手持物当获取，不新建执行器或放宽权限。[合同](museum-local-method-view.md)。
- 真实公开Build/Verify同SHA`70fe76b7...`，原覆盖精确验证corpus路径/当前视图哈希及独立重建证书后，本条件方法executable，比较/native均train、ready=false。28.15秒完成当前源Build/Verify/两份原覆盖，另18criterion/10method未变、原输入未变；源集不带无关冻结root，仍保留19项完整分母。覆盖直接复用已验corpus，协议端测试防重复深验；37相关检查含14新检查、4公开伪全根/删获取节点/错nonce/覆盖探针通过，最终回归/CI见收口。
- 最终Core离线2354/Backend629零失败零跳过，最终代码重放同SHA、EVD-338/差异检查通过。代码/测试/文档之外均仓库外，不提交view/raw/corpus/local-data；无游戏、训练、worker/API或服务器变更；最终CI见收口。
- **唯一下一步：** 只新增真正缺失的validation/test全馆终态来源；先按原save/player/day验分区，再复用E隔离运行时/原捐赠链/95项reader/比较/join/corpus/本证书及原覆盖，不重采B或旧根、不借policy日期分区。冻结并集仍`12/19、12/19、12/19、9/11 roots`，formal=false；全95新档获取/RNG重试/共享承诺/第三年前全局预算与Teacher21/Student义务未完成，Money6随后。

## 2026-11-07 全馆criterion corpus与原覆盖（当前唯一接续）

- #479经CI `42299920297`全部38步接受、合入`33b74801`。新corpus复用B原完整typed证明一次，不重复读取native链或经过策略轨迹；95项、原pairs/候选数、唯一原生动作/nonce、库存与原决策分区保留，重复来源/组/run/nonce和混权威拒绝，不改旧权限/执行器。[合同](museum-criterion-corpus.md)。
- 真实公开Build/Verify同SHA`c7e75ad4...`，1来源/1pair/1原生终态，仅train；原覆盖已接`museum_terminal_corpus`，方法pending/ready=false。同代码两份原对账另18criterion和10method完全相同，四个已固定来源哈希未变。外部探针误从criterion行取分区，只按旧method行合同修探针并验证已有报告，未重跑来源或反改旧格式；23新专项通过，最终反例/回归/CI见收口。
- 最终Core离线2340/Backend629零失败零跳过，4类公开伪全根/删项/重复join/覆盖探针拒绝、源不变；最终代码重放仍同SHA，EVD-338/差异检查通过。只提交代码/测试/文档，不提交corpus/来源/raw/local-data；无新游戏、训练、worker/API、服务器或文件清理，最终CI见收口。
- **唯一下一步：** 独立条件局部方法视图及原证书接续，再真正缺失的validation/test。保留完整原方向/阻塞与95获取、RNG、共享资源和第三年前全局预算义务，不以终局声明前置冒充获取、不重采B或已验根、不用corpus自行晋升method。并集仍`12/19、12/19、12/19、9/11 roots`、formal=false；全新档Teacher21/Student未完成，Money6随后。

## 2026-11-07 全馆统一输入与原准入（当前唯一接续）

- #478经CI `42294539442`全部38步接受、合入`b4baed10`。本轮仅离线复用B，接95项共享模型输入和公开可重建准入报告；原编译器/执行器/注册权限/训练validator/provider均不改。[合同](museum-policy-input-admission.md)。
- 原轨迹builder真实拒绝捐赠训练行后，沿旧Collection的evidence-only分支，而不是削弱旧代码：捐赠EvaluationOnly/需确认，600tick等待是参照不是原策略候选。原生save/player/day和policy日期分区各自保留；未提交的无效provider/schema草稿已撤。报告真实1候选/1参照、全95输入、两项阻塞，policy_row_built/formal均false，原始数据仓库外。
- 最终代码Core2317/Backend629零失败零跳过，原EVD-338/差异检查通过；4类公开篡改/覆盖探针拒绝、源未变，最终重放SHA仍`03b58baa...`。新增输入投影每调用只绑定一次快照，状态/候选复用同实例，不增全量快照或路径缓存。新后端夹具漏字段元数据只修夹具，不改入口校验；最终CI见收口。
- **唯一下一步：** 复用原Rusty证据型criterion corpus/条件方法模式接全馆native证明及原覆盖，再补真正缺失分区；不为制造训练行停工、不重采B或已验根，不增加第二套执行链。需要策略训练时必须显式区分隔离研究授权与产品捐赠许可，不可用native成功倒推授权。并集仍`12/19、12/19、12/19、9/11 roots`，formal=false；完整95获取/RNG/共享预算/Teacher21与Student义务保持，Money6随后。下方轨迹/provider下一步假设仅为已校正历史。

## 2026-11-07 全馆typed联合证明（当前唯一接续）

- #477经CI `42291144137`全部38步接受，合入`2e082394`；原比较及成功B源不重做。唯一primary仍全馆，并集`12/19、12/19、12/19、9/11 roots`及formal=false保持，Money6随后。
- 新typed Build/Verify各只重建原比较一次、原强整队列一次，再以已验digest复用元数据；原编译/原始wire/nonce/环境/完整逻辑快照/同tick与日期全部绑定。复用原95项reader核对终态，保留全95前后项及原pairs、精确槽位/品质/库存一件消耗、成就5真实变化和菜单退出，不改旧reader或执行器。[合同与来源](museum-terminal-native-join.md)。
- 真实公开重建同SHA`aa961fc4...`：95项/1pair、train/day209、tick126→331、205跨度/204原生遥测、slot11栈2→1、94→95及成就5 false→true。70项专项含22新检查及最终完整Core2305/Backend621零失败零跳过；新合成夹具漏quality只补新夹具后全量重跑，不降真实校验。4类公开伪全根/跨run/错后态/覆盖探针拒绝、原源未变，最终代码真实重放仍同哈希；构建零警告/错误、原EVD-338/差异检查通过，最终CI见收口。
- **唯一下一步：** 用本已验来源接原trajectory/provider及统一输入，再criterion corpus/条件方法/原覆盖与真正缺失分区。现有typed源不等于三分区、全根或完整新档Teacher21/Student；获取/RNG/共享资源和第三年预算义务不减。不重采B/已验根，无新游戏/worker/API/服务器/产品训练/清理，原始产物及local-data仓库外。

## 2026-11-07 全馆独立终局比较与原生来源（当前唯一接续）

- #476经CI `42286239023`全部38步接受、合入`5694c6de`，原95项入口不重做。唯一primary仍`complete_museum_collection`；并集`12/19、12/19、12/19、9/11 roots`与formal=false不变，Money6随后。
- 新比较先验完整intake，再用注册账本重评完整捐赠候选，复用原去模型分数/诊断/时钟/计划与队列编译及稳定身份helper；只比较安全94→95与原等待primitive的600tick反事实，两边均编译通过才产pair。等价副本不强排、拒绝项保留，显式选中preferred候选才编译；原Rusty59→60/全局Collection Teacher/执行器不改。[合同与来源](museum-terminal-teacher-comparison.md)。
- B后台静音真实1候选/1pair，公开比较重放同SHA`c9ab2e0c...`，原队列完成94→95、成就5 false→true、slot11栈2→1；原强整队列reader通过1动作/10源digest，原档未变、进程退出。A历史原档实际第5年，在动作前正确拒绝；只修新调用者的隔离日期边界。新库存探针误假设夹具1件，只修探针、不改旧夹具或重跑成功B。原始数据仍仓库外，不把前94件/持有物/日期/钥匙奖励重置当获取。
- 48项实际专项含17新检查及最终完整Core2283/Backend621零失败零跳过，构建零警告/错误、原EVD-338/差异检查通过；3类公开伪native/等待派发/覆盖探针拒绝且源未变，最终CI见收口。**唯一下一步：** 离线复用B接typed全馆比较/native联合证明，再原轨迹/provider/corpus/条件方法/覆盖及真缺分区。不重采B或已验根，不把原生单源当整项或Teacher21/Student完成，完整新档获取/RNG/共享预算责任不减。

## 2026-11-07 全馆局部接入（当前唯一接续）

- 婚房#475经CI `42283350602`全部38步接受，合入`fea19c7a`，对应分支已清理；并集冻结`12/19 executable、12/19 split-complete、12/19 ready、9/11 roots`，formal=false。唯一primary改为`complete_museum_collection`，Money6随后，已验婚房/Rusty不重采。
- 新薄入口复用原95项严格reader、原快照/身份helper与原捐赠候选，不改旧全局Collection Teacher、桥或执行器，不要求其他收集root先完成。区分馆藏齐全与成就5持久事实，保留原依赖方向和不可用候选；不证明资源预留、不产标签/native/全根或训练许可。[合同、来源及接续](museum-collection-local-intake.md)。
- 真实保留源公开Build/Verify同SHA`5be9d272...`，95行/已54/缺41/成就false，实际当前捐赠候选0如实保留；伪标签与覆盖写入公开探针拒绝，原输入未变。31项实际针对性检查及最终完整Core2266/Backend621零失败零跳过，构建零警告/错误、原EVD-338与差异检查通过，最终CI以本PR收口为准。本轮不启动游戏/worker/API/服务器/训练/清理，原始数据仍仓库外。
- **下一步：** 先复核既有局部比较/反事实组件，再经原候选和编译器建立全馆独立比较，接94→95同run原生来源及原轨迹/corpus/有条件方法/覆盖。94件及最后一件持有可作测前声明、不可冒充获取；完整新档获取/RNG/跨目标共享预算与Teacher21/Student责任不减，不复制执行器或把其他收集整根反挂为局部前置。

## 2026-11-07 婚房缺失分区收尾（当前优先）

- #474已由CI `42272593550`全部38步接受，合入`cc61694b`。本轮仅补validation/test，原C、Friendship、局部方法和旧执行器不重做。validation冬21为216→219、2准备/11队列/12动作；test冬12为207→210、4准备/15队列/16动作，均完整原生求婚/三次睡觉/婚礼、原档不变、进程退出。
- 原policy在启动前确定分区，婚礼日不另计。完整corpus为8行/3结果，原分母83（test索引2为80）、各1pair；原数据集8接受/0拒绝/0重复、2/2/4、即时0且长期全pending。原覆盖method executable、比较/native三分区、criterion ready/本项open work为空；其他18项完全一致，原输入哈希未变。失败冬17新夹具跳过原生日程触发点，只修仓库外调用者，不改旧候选/桥/执行器。[来源、归因及边界](marriage-local-acceptance.md)。
- 本项被原数据集/覆盖及CI接受后，接受并集才为`12/19 executable、12/19 split-complete、12/19 ready、9/11 roots`；formal=false，完整新档Teacher21/Student及获取/共享预算责任不减。随后唯一primary是`complete_museum_collection`，先核对95项权威事实并复用已验Rusty捐赠/预留/保管/路径/滚动方法，再补真实缺口；Money6随后。不重写捐赠执行器或重开已验Rusty。

## 2026-11-07 婚房有条件局部方法证书

- #473已由CI `42268173931`全部38步接受，合入`383ba26b`、对应分支清理。corpus/原行/完整C不重做。本轮新独立局部视图复用原corpus验证和完整原生链，实际Build/Verify同SHA`8188644c...`；原依赖图文件不改，原方向/阻塞/递推及所有其他根完整保留，来源和前置不冒充获取。
- 礼物是原`gift_partner`准备依赖，不扩张目录直接婚房动作；直接路线绑定原生求婚控制及其精确candidate/queue/state，全部日结算/剧情控制保留、不作偏好标签。原队列audit没有逻辑option_id，只读已验原生身份，不臆造字段。覆盖先验corpus，再复用已验对象独立重建精确证书，防手改complete/错源/换哈希，不增加第二次native深验。[合同、边界及接续](marriage-local-method-view.md)。
- **唯一下一步：** 原分区policy先确定日期，再仅采真正缺失的validation/test完整婚房联合来源，经原行/corpus/局部证书/覆盖收口。不要重采train C/Friendship或把婚礼跨日另计分区。接受并集`11/19、11/19、11/19、8/11 roots`及formal=false暂不晋升；完整新档获取、Teacher21/Student及全局日程资源义务不缩减。本轮无新游戏/worker/API/服务器/产品训练/清理，原始数据仓库外，最终同版本对照/回归/CI见收口。
- 最终实际同代码对照：婚房method=`executable_frontier`，比较/native仍仅train，open work仅缺失分区的比较与原生来源；其他18项criterion完全相同。3类公开伪全根scope/错corpus/覆盖探针均拒绝，伪scope在完整合法重建后才全工件不符，原输入哈希未变。最终完整回归/CI见收口。
- 26项新检查及最终Core2235/Backend621零失败零跳过，EVD-338与差异检查通过；只提交代码/测试/文档，不提交原始数据、局部视图或local-data，最终CI/发布以本PR收口为准。

## 2026-11-07 婚房criterion corpus与原覆盖接续

- #472已由CI `42262971311`全部38步接受，合入`1503473e`，对应分支清理。原Teacher行/完整C不重做。本轮按完整joint来源分组，组内一次深验后复用全部原准备行重放，不按路径跨调用缓存；保留全部队列/动作/nonce与完整分母，不改变执行器或原行奖励。
- 实际公开corpus Build/Verify同SHA`62c4af46...`：两次准备决策只记一次结婚结果、11队列/12primitive、各83候选/1pair/81未比较；输入逆序仍还原原索引，整组沿原决策分区，仅train。婚礼跨日不另算新分区，已有房屋/好感/物品不是获取。原覆盖/对账已接`marriage_joint_corpus`，方法责任不清除，最终同版本基线/反例/回归及CI见本PR收口。[完整合同及实际诊断](marriage-criterion-corpus.md)。
- 新探针最初用到Friendship接入前的历史Skills基线，既有Friendship诊断变化不算回归；只修新探针基线并接续未完检查，不改旧对账或重复成功原生。**唯一下一步：** 独立有条件婚房局部方法/证书，然后只补真正validation/test联合来源并原覆盖收口。接受并集`11/19、11/19、11/19、8/11 roots`及formal=false保持，完整新档Teacher21/Student与全局预算义务不缩减，无新游戏/worker/API/服务器/产品训练/清理，原始数据仓库外。
- 最终同代码基线对照另18项criterion完全相同；婚房比较/native均仅train、method仍pending。4类公开伪formal/重复行/漏控制/覆盖探针均拒绝，漏控制在最后代码完整重建合法来源后才因全工件不符拒绝，原输入哈希未变。37新检查及完整Core2209/Backend621零失败零跳过、EVD-338/差异检查通过；最终CI/合入以本PR收口为准。

## 2026-11-07 婚房原Teacher行接续

- #471已由CI `42256249915`全部38步接受，合入`cf62d769`；原完整C不重采。当前婚房接原`trajectory.v2`、显式provider及原数据集/训练器，不新造格式或执行器。每行83候选/1严格pair/81未比较，机械控制只链接完整joint来源，不另造标签；即时奖励0，全部长期回报pending。
- Teacher/Backend/loop反馈复用同一婚房输入投影，现有contact-only组件与原1975点目标分离。实际接口探针发现第二行的非赠礼对话漏接触认证，只修新共享投影、离线重建b版，原比较/原生源和旧执行器不改。新schema独立source/index/hash，不借CC/Skills/Friendship证据，旧行缺省序列化不变。完整回归/CI以本PR收口为准。[合同及实际诊断](marriage-policy-teacher-rows.md)。
- b版行SHA`a8179104...`/`8db29f20...`，抽样公开重放同字节；与旧Friendship行经原CLI混合接受3/0拒绝、全部train/长期回报pending，manifest`4cdd70d1...`。真实后端两次HTTP200、各83候选特征逐项相同（仅零权重测试夹具，不是训练）；24项新Core/4项新Backend检查及完整Core2172/Backend621零失败零跳过，EVD-338与差异检查通过，最终CI见收口。
- 5类公开负索引/缺源/借用schema/伪joint哈希/覆盖写入探针全部拒绝，无成功输出、原正例与来源未变。原数据仓库外，最后代码不改变native执行器；新provider每次重放完整来源，不使用仅按路径的跨调用不安全缓存。
- **唯一下一步：** 原criterion corpus/覆盖登记、条件局部方法与真正缺失分区；不重采已通过C或Friendship，不把原有房屋/初始关系/夹具物品记成获取。并集`11/19、11/19、11/19、8/11 roots`与formal=false不变，无worker/API、服务器、产品训练或清理，原始数据仓库外。

## 2026-11-07 婚房typed联合原生回执

- #470已由CI `42246004755`全部38步接受，合入`3cb31d8a`，对应分支已清理；C完整原生来源不重采。当前唯一primary仍婚房，并集`11/19、11/19、11/19、8/11 roots`及formal=false不变。
- 新Build/Verify逐项复用原强reader核对C全部11队列/12primitive、原始gzip请求/nonce/事实/完整快照，再验证原准备比较与编译、正常礼物计数/消耗、原求婚日历、逐日睡觉与婚礼归位。旧native门、执行器和公共编译准入不改；内部返回已验源避免重复比较。两次各1pair/81未比较完整保留。
- 当前源已婚标志在211→212原生睡觉的婚礼设置阶段置位，剧情结束另记；婚礼原生回调归位与普通被动位置不混淆。新消费层24tick/可读current_tool的过严假设已据原代码及源修正：有限更新tick需钟/资源不变，旧桥unavailable/null保留，不臆造无工具；婚礼主屋归位严格核对既有只读入口投影，不使用魔杖、不改变旧剧情完成标准。[完整合同及归因](marriage-native-joint-receipts.md)。
- 真实公开Build/Verify同SHA`c199b006...`；6类漏睡觉/未演完/跨run/错偏好/伪许可/覆盖写入探针拒绝，原源与正例未变，50项新检查及最终Core2148/Backend617全部通过、零跳过，EVD-338/差异检查通过，最终CI见本PR收口。原始数据仓库外，无新游戏、worker/API、服务器、产品训练或清理；已有三级房屋/初始关系/物品仍仅声明前置，不晋升整项。
- **唯一下一步：** 离线用本typed源接原trajectory.v2 Teacher行、显式provider和统一输入，再原criterion corpus/覆盖、局部方法与真正缺失分区；不重采成功C/Friendship，不借CC哈希或改旧1975门，不把条件native源当完整新档Teacher21/Student。

## 2026-11-06 婚房同run原生链与后台对话修复

- #468/#469已接受合入`1bb9b40e`/`2ff53d0f`，原比较、用途协议和Friendship不重做。唯一primary仍婚房；接受并集`11/19、11/19、11/19、8/11 roots`、formal=false保持。
- A实测赠礼后普通对话601tick超时。先核对`45645d96`及原聊天JSON，再新反编译确认原版无焦点菜单只更新、不派发输入；仅隐藏且命中该原生焦点门时复用既有native menu click。普通对话/说话者/问题/动画和次数门、前台SMAPI输入均保留，不强制关菜单、不改控制标志、不抢系统鼠标。B实际2次点击/82tick关闭，原求婚及3次逐日睡觉通过。
- B到婚礼时的新调用者误走普通菜单恢复，旧恢复器正确拒绝。只修仓库外调用端，先用B保存快照预检原剧情编译，再C单run经原赠礼、求婚、回家、睡觉和剧情执行器闭合：2赠礼队列+9控制队列，209→210→211→212，已婚Abigail/房屋3级/联合评分=true，剧情与菜单退出。C为完整来源，不拼接B或G丢失内存；原房屋/好感/夹具物品仍是已声明前置，不冒充获取。
- C原档不变、游戏/后端退出；原强reader抽样通过关菜单、求婚、跨日及剧情四类回执。Core2098/Backend617零失败零跳过、13项专项、E Debug零警告/错误、EVD-338通过；CI/合并以本PR收口为准。原始数据仍仓库外，无worker/API、服务器、产品训练或清理。[完整来源及范围](marriage-current-comparison.md#same-run-joint-outcome)。
- **唯一下一步：** 离线消费C完整11队列，复用原强reader做婚房typed联合回执，再接原trajectory/provider/corpus、局部方法与真正缺失分区。不要再重采已通过的C或原Friendship，不把原生单来源、礼物准备pair、已有三级房屋当完整新档Teacher21/Student或覆盖晋升。

## 2026-11-06 婚房独立准备比较（本轮接续）

- #467已由CI `42224462036`全部38步接受并合入`baa9a28d`，原日历/非终态起点不重做。唯一primary仍婚房；接受并集`11/19、11/19、11/19、8/11 roots`和formal=false保持。
- 新比较消费端复用原5族完整intake、原材料/货币预留投影、社交接触预算与原编译器；不改旧1975点Friendship绑定器。原图gift依赖角色及婚房goal保留。同伴侣/同原生效果下比较实时出售价值成本，不冒充未登记未来用途的全局最优；其他候选/未知保留，显式pair准入不等于完整排序。房屋施工可并行准备伴侣，已有订婚/婚姻不重复准备。[完整合同与接续](marriage-current-comparison.md)。
- 真实F已得1个独立pair：2420→2500、完整到达预算同18:56，紫水晶100对南瓜352；81项不可比较保留。E新调用者未请求物资域、F新用途尚未在后端登记，均在被测移动/赠礼队列前拒绝；不是旧执行器错误。新增显式`marriage_teacher`只在原social_future之上读取缓存物资图，旧用途不扩大。真实F已先通过后端HTTP200预检，再开G。
- G已完成原队列农场→森林、鲜态重绑后的移动+赠礼，共2队列/3原生primitive，实际2420→2500、slot9消耗，南瓜/吊坠保留。两次各1pair/81未比较，公开Verify同SHA`8b273abe...`/`912bc138...`，3类完整排序/伪原生/覆盖探针拒绝，原档不变、游戏/后端退出。旧账本`source_state_hash`为上次修改来源，按原持久化语义用鲜态物资图投影，不要求重写历史；新消费端已修正，旧账本服务不改。
- 最终Core2097/Backend617零失败零跳过（共40项新检查），E桥Debug零警告/错误，原EVD-338及差异检查通过；用途协议与比较消费端分开PR，最终CI/合并见收口。强制快照短样本50 updates/s不是流畅性PASS，不逐帧采集。无worker/API、服务器、产品训练或清理，原始数据/local-data不提交。
- **唯一下一步：** 原求婚/跨日结算的同run完整来源与typed回执、原轨迹/provider/corpus/局部方法及真正缺失分区。G已退出，不能拼接丢失内存；完整joint场景复用已验前置和原赠礼/求婚/通用native-save-boundary控制（`control_plane.native_save_boundary=true`）在单run闭合，不另写睡觉执行器或在标签后传送。旧已验Friendship/校准不重采，不晋升为完整新档Teacher21/Student。

## 2026-11-06 婚房原生日历与非终态起点

- #466已由CI `42212450279`全部38步接受并合入`7ac78b1e`，原当前事实/5族完整接入不重做。当前唯一primary仍婚房；并集`11/19、11/19、11/19、8/11 roots`及formal=false不变。
- 新桥只追加原生婚礼日历，实时反编译确认婚礼需避开节日及种子绿雨；112天谓词按种子/年份/节日集合缓存，不逐帧扫描。新条件投影区分采购/订婚和实际跨日结算，保留第三年期限、未知和待原生日阶段，不产生授权或标签。旧GSQ/执行器/Teacher语义不改。
- E隐藏静音非终态D已确认原生日程到SeedShop(20,5)，原档不变、进程退出。A/B是新夹具的已婚NPC序列化归属/跳时钟遗漏日程问题，C是新调用者越过3片等待上限；只修新夹具和调用端，不反改旧工具。D完整保留533礼物行，原生两项love+80；仅前置，不记婚房完成。45项针对性测试通过，完整回归/CI以本PR收口为准。[来源、性能限制和接续](marriage-house-local-intake.md#native-calendar-and-nonterminal-boundary)。
- **唯一下一步：** 独立当前婚房比较，复用原校准/完整接触预算，先证明物品与跨目标资源承诺，不按礼物价格臆造全局优劣；新run鲜态重绑后接原赠礼/求婚/日结算与原轨迹/corpus/覆盖。不得重采已验友谊或把本轮条件日期当Teacher总验收。
- 最终本地Core2067/Backend607零失败零跳过；公开投影重放同SHA`1ce29acf...`、原intake Verify一致，拒绝覆盖且输入不变。D原10个非空库存位和全部关系点数/状态/每日计数保持；EVD-338和差异检查通过，最终CI/发布见本PR收口。原始来源均仓库外，无worker/API、服务器、产品训练或清理。

## 2026-11-06 婚房当前事实与原候选接入

- #465已由CI `42208557116`全部38步接受并合入`c0e7cc80`，该PR分支已清理。Friendship三分区不重做，并集正式保持`11/19、11/19、11/19、8/11 roots`及formal=false；当前唯一primary是`marriage_and_house_upgrade`。
- 新只读接入复用原桥、时钟、方向目录与完整离线evaluator，从原未scoped图发现婚房/伴侣/对话/礼物/日结算5族，保留整方向和全部不可用原因。当前player/world事实、日期、钱/房屋/施工及联合评分必须一致；现有室友不冒充待入住，玩家间婚姻不强制NPC配偶。原报价保留，精确合法性仍由旧候选/编译器判，不改旧执行器或房价表。[本轮合同、复用表与接续](marriage-house-local-intake.md)。
- 实际公开Build/Verify同SHA`9076133d...`，原二年冬28源已婚/三级仅记当前满足；5族来源可读，gift410/talk43（不可用410/13）全保留，源未变，label/native/formal全false。新组合夹具缺少旧测试无需的日期/版本/source元数据，只补新夹具、不改旧helper/时钟/校验器。最终Core2022/Backend607零跳过（46新检查）、原EVD-338与差异检查通过；3类公开伪标签/漏礼物族/覆盖写入均拒绝且无新输出，CI/发布见本PR收口。无新游戏/worker/API/服务器/产品训练/清理，不提交local-data。
- **唯一下一步：** 独立当前婚房比较消费端，先非终态隔离边界与真实可比较替代项，复用原完整社交接触/房屋/伴侣/日结算。采购不等于升级完成、订婚不等于结婚，必须保留第三年期限和跨日资源承诺；不能用当前已满足源造进展标签，不能造候选/负例，再接同run原生及原数据覆盖链。

## 2026-11-06 Friendship三分区局部验收收口

- #464已由CI `42203227382`全部38步接受，合入`7005c835`；原方法视图、train两行和真实校准不重做。本轮仅补5/10各自validation/test四个E后台静音来源，实际人数4→5、9→10，原存档均不变，游戏与后端已退出。
- 日期先按原policy确定：二年冬21为validation、冬17为test；仅显式声明的隔离副本日历/daysPlayed可调整，原9参数构造及缺省日期/JSON保持，原source/basis日期门仍先执行，原生加载日另校验。匹配DLL反编译及实际Evelyn日程已核对，不改旧Teacher/候选/编译执行器。
- 六行原corpus SHA`ec7c58b8...`、原数据集接受6/0拒绝、每分区2行，长期回报全pending；每行保留29候选，实际pairs为5/5/5/5/4/3。five-test原编译仅需talk一项，不伪造移动或统一队列长度。原覆盖两项ready、对账无本项open work，另17项完全相同，完整原方向及源哈希保留。[完整验收与下一步](friendship-local-acceptance.md)。
- 本轮汇总探针最初误读method不存在的`coverage_gate_ready`，按旧合同`primary_disposition=coverage_ready`修正新探针；不改旧对账、不重跑成功游戏或深验链。13新日历检查，最终Core1976/Backend607零跳过，243脚本AST、原EVD-338及差异检查通过，最终CI及发布见本PR收口；只提交代码/测试/文档，原始数据留仓库外，无worker/API、服务器、产品训练或清理。
- **发布及唯一下一步：** 最终回归/CI接受后并集为`11/19 executable、11/19 split-complete、11/19 ready、8/11 roots`，formal=false，非完整新档Teacher完成。剩余婚房1、全馆1、Money6；按当前复用增量选择`marriage_and_house_upgrade`。先严格当前事实/完整候选接入与旧实现复用表，再独立比较、同run原生/原轨迹/corpus/方法/真正缺分区，不另造婚姻或房屋执行器，不把全新档责任倒挂初始接入。

## 2026-11-06 Friendship有条件当前来源方法视图

- #463经CI `42199313265`全部38步接受，合入`33219b42`、分支清理。corpus/原数据行/provider/来源登记完成，不重做；唯一primary仍`raise_friendships`，接受并集`9/19、9/19、9/19、7/11`及formal=false保持。
- 新公开Build/Verify复用已验corpus，派生独立Friendship局部视图，保留完整原方向/衰减/未来接触/十人日历与21分竞争义务、全部旧节点和其他根；原图仍pending，既有Skills文件/消费者不改，不叠加局部视图。新层最初误要求目录所有方向都有依赖图，专项据旧加载器修正为已有已知唯一图集合，不反改旧格式。
- 实际公开视图同SHA`0123b982...`；原coverage/对账在验证精确corpus/整视图证书后，两项method executable、各只有比较/native train、ready=false，另17项完全相同、输入未变。完整四项动作/nonce、原选项Registry字段及新态重绑要求留在证书，不重复native深验、不把未比较项当负例。[方法范围、来源与边界](friendship-local-method-view.md)。
- 最终Core1963/Backend607全部通过、零跳过（26新检查、79专项）；原覆盖入口的手改scope/同字节错配corpus和覆盖写入3类公开抽样均拒绝且无新输出，原图/来源未变，原EVD-338与差异检查通过。最终CI/发布见本PR收口。
- **唯一下一步：** 先按原policy分区选真实日期，只补5/10各自validation/test四个E隔离来源，再原行/corpus/局部视图/原覆盖收口；train与真实校准不重采，不手改标签或删候选，不重开已接受步骤。当前无新游戏、worker/付费API、服务器、产品训练、原始数据/local-data提交或清理；最终回归/CI见本PR收口。

## 2026-11-06 Friendship criterion corpus与原覆盖登记

- #462经CI `42195611122`全部38步接受，合入`94e3dcad`、分支已清理；原Teacher行/provider/统一输入完成，不重做。唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`及formal=false保持。
- 新typed corpus从原行Verify一次重放完整native链，再只读取已绑定元数据；保留每项队列/primitive/nonce数组、完整候选分母与5pairs/未比较范围。原19项21分/1975点人数规则及5/10各自criterion绑定，复制动作/决策/nonce、借阈值/分区均拒绝，不把末次社交当整链或推定先前获取。
- 实际两行corpus公开Build/Verify同SHA`7c5b0dbe...`，各仅train；原coverage及reconciliation已登记新来源，两项比较/native有train，method pending、ready=false，另外17项逐项不变、输入未变。旧完整新档/衰减/跨图与21分竞争预算保留；corpus不冒充方法证书，最终回归/CI见本PR收口。[完整协议与来源](friendship-criterion-corpus.md)。
- 最终Core1937/Backend607全部通过、零跳过（35新检查）；4类公开criterion/移动nonce/formal/覆盖写入抽样拒绝且无新输出，原Teacher行哈希仍`0e805776...`/`c8ec56d0...`，原EVD-338与差异检查通过。最终CI/发布以本PR收口为准。
- **唯一下一步：** 复用该corpus接有条件当前来源方法视图与原frontier/coverage证书，再只补两项真正缺失的validation/test；不重采已验train/校准、不重开行/provider，不削减完整Teacher责任。无新游戏、worker/付费API、服务器、产品训练、原始数据/local-data提交或清理。

## 2026-11-06 Friendship原轨迹与统一输入

- #461经CI `42145083661`全部38步接受，合入`3ba9796f`、分支清理。两份强原生来源不重采；唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`和formal=false保持。
- 新Friendship行Build/Verify接原`policy_decision_trajectory.v2`、完整原生队列和显式身份provider；专属native来源不借CC哈希，原始5pairs/未比较19及15/完整排序false保留。已验的接触许可逻辑原样移到Core供新消费者复用，旧候选/默认窗口/编译执行/原单动作门不改。
- 行、Backend结构化推理、loop反馈共用原生人数及完整接触到达预算投影；模型专属`policy_features`不进入动作参数，不重复已编码点数/delta。未知不当零、首段不当全程；原Skills和无关goal输入保留，loop复用已有显式校准参数及有效排名目标/原队列核对，不要求旧command的可空goal字段，不重新发现F盘。[协议和范围](friendship-policy-inputs.md)。
- 两条真实行公开Build/Verify同SHA`0e805776...`/`c8ec56d0...`，各29候选/5pairs/2队列项/原train；与旧Collection及两条Skills共5行被原数据集接受、0拒绝、长期回报全pending。真实后台直达及Grandpa转入Friendship两路各29候选输入逐一相同，8类CLI篡改/覆盖拒绝、源未变；探针最初误要求拒绝码2，旧接口返回1且正确拒绝，只修外部断言。最终Core1902/Backend607零跳过，36新检查，原EVD-338及差异检查通过；最终CI/发布见本PR收口。
- **唯一下一步：** typed criterion corpus接原coverage/reconciliation，再有条件局部方法与真正缺失分区；当前真实day223两行不等于三分区，不晋升整项。无新游戏、worker/付费API、服务器、产品训练、原始数据/local-data提交或清理。

## 2026-11-06 Friendship两步强回执已接入

- #460经CI `42140897990`全部38步接受，合入`6a35d0b8`、分支已清理；两次原生人数阈值不重采。唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`和formal=false不变。
- 核对原提取提交`3bde2048`及既有消费者：旧单动作Read要求编译态绑定动作前态，是既定合同；保留该入口/返回类型/默认严格门。新增整队列入口先走原完整queue validator，再私有复用原wire、actor、参数、nonce及快照检查；第二项只作为已验证整链的frame，不冒充旧独立动作证明，不改旧执行器或写回原队列。归档JSON哈希查询无消息，不捏造旧理由。
- 新typed Friendship Build/Verify重放原比较/编译一次，核对完整两项队列与每个native请求/逐帧/事实/环境、目标+20点及精确人数，其他NPC和移动不能记功。实际h与ten-a来源离线公开重建b版分别同SHA`ff353d1e...`/`77f215a8...`，各5原始pairs和19/15未比较保留、完整排序false；两项criterion各自有强原生阈值，但尚无训练轨迹/provider/corpus/整项晋升。8类公开篡改拒绝且无输出，原始数据不改写。[协议和来源](friendship-native-receipts.md)。
- 最终完整Core1876/Backend597零跳过（含107专项）；最终原始解包JSON全帧比较及未建模根字段拒绝后，两份公开Verify仍同b哈希；原EVD-338/差异检查通过。无新游戏，最终CI/发布见本PR收口。
- **唯一下一步：** 复用这两份强来源接原`policy_decision_trajectory.v2`和显式pair replay provider，再criterion corpus/原覆盖；在行、Backend、loop统一补实需native人数/完整接触预算投影，不重写已编码候选参数或无关goal，不重采成功动作/校准、不借分区。当前day223两份来源不等于三分区；局部方法及后续真缺分区仍按原验收责任。回归/CI见本PR收口；无新游戏、worker/API、服务器、产品模型训练、原始数据/local-data提交或清理。

## 2026-11-06 Friendship真实人数阈值来源

- #459经CI `42132500513`全部38步接受，合入`1f60faa7`、分支已清理；显式成对准入不重做。唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`及formal=false保持。
- 新隔离边界准备器复用原安全文件层，仅在副本声明4/9人前置、1955点对话目标、临时起点及当地剧情前置；源档、婚姻/库存/礼物计数保留，不把预置好感或剧情当成果。新后台入口复用原时间夹具、原生有界等待、完整比较/Verify及原两步预编译队列；旧debug时间只改钟、Evelyn早晨游走都按原契约消费，不改旧调度/候选/执行器。
- 核对`85d42661`/`007eb7c8`后，仅新比较层增加“本地talk优于已证完整跨图talk”的窄几何适配；原进度/资源/时间无退步标准保留，缺证明、反向首段比较、礼物/未知类仍拒绝。policy显式v2，旧工件须原输入重建；原archive哈希查询无消息，不捏造历史理由。
- 真实E后台两次成功：`friendship-five-20261106-h`的4→5（tick1877→2003），`friendship-ten-20261106-a`的9→10（1877→1999）；均Evelyn1955→1975，原移动＋社交队列applied、fresh、selected completed。完整gift410/talk43保留，各5 strict pairs，未比较19/15仍保留、完整排序false。公开Build/Verify同SHA分别`38e29d29...`/`0326f83f...`；源目录SHA`0180b041...`退出后不变。早期a-g是动作前拒绝的夹具/消费端诊断，不算七个执行器bug；全部原始产物在仓库外。[来源、前置、边界](friendship-native-thresholds.md)。
- 最终完整Core1860/Backend597零跳过，243脚本AST、原EVD-338及差异检查通过；另含声明数组所有权、跨几何NPC别名及旧policy拒绝，最终离线Verify不重采游戏。游戏与后端已退出。发布以最终CI/收口记录为准。
- **唯一下一步：** 离线复用这两次原始来源，接原强native action reader和完整两项queue receipt validator，再原轨迹、显式pair replay provider、criterion corpus及覆盖/对账；各自只是真实day223的一份来源，不能借分区或提前晋升整项。核对native人数/完整接触预算在行、Backend、loop的统一投影，不重写已编码的候选参数，不重采成功动作/校准。当前仍`pending_typed_receipt_join`，不是已入训练行。完整回归/CI见本PR收口；无worker/API/服务器/产品模型训练/清理，不提交原始数据/local-data。

## 2026-11-06 Friendship显式成对监督准入

- #458经CI `42129422453`全部38步接受，合入`f8b93506`、分支清理；原跨图预算不重做。唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`及formal=false保持。
- 新v4工件显式区分成对标签可用与完整排序可用；缺省完整排序门、原单参数Compile接口保留，只有typed/CLI显式`independent_pairs`才消费已重放strict pairs。build/Verify/compile共用scope选择及原编译，未知mode拒绝；不改原Teacher/比较数学/候选/执行器/共享trainer，不把未比较项当负例。旧v1-v3保留，须从原来源重建，不覆写冒充升级。
- 原真实来源公开入口：缺省Build退出2，显式Build/Verify退出0，三份完整工件同SHA`4581deab...`；gift410/talk43、30决策/8pairs/20未比较均保留，full=false/pairwise=true。默认compile拒绝且无输出，显式compile产生原单连接器pending队列，目标仍Lewis；非法mode无输出。无NPC动作/训练行/覆盖晋升。完整Core1833/Backend597零跳过，AST/EVD-338/差异检查通过；最终CI见本PR收口。[协议与退出边界](friendship-pairwise-admission.md)。
- **唯一下一步：** 接E副本中真实4→5、9→10人数阈值，声明边界前置、完整保留候选/未比较项，复用原机械队列runner/社交原语/真实校准，再原native reader、轨迹/provider/corpus/覆盖三分区。旧Linus1000重置夹具不能冒充此阈值。入行时复核显式native人数/完整接触预算投影，候选点数/delta/portfolio参数原encoder已编码，不误判全缺，不改无关goal特征。当前尚无新游戏，不能因显式编译通过晋升整项或正式训练；不提交原始数据/local-data，无worker/API/服务器/清理。

## 2026-11-06 Friendship跨图完整接触预算

- #457经CI `42125618154`全部38步接受，合入`75e7d9a0`、分支清理；完整候选源与真实校准不重采。唯一primary仍`raise_friendships`，`9/19、9/19、9/19、7/11`及formal=false保持。
- 新只读消费端复用原`RequiredFirstEdge`路线证明/接触窗口，绑定实际首段、完整尾程、原生时间窗与预算；每实例加载一次context并按完整请求缓存。原远端即时站位留空/一次连接器后fresh-replan是旧设计，不改候选、Teacher、绑定、编译或执行器。v3完整重放拒绝旧版本/坏身份/伪造窗口及路线；不把首段当全程。
- 原真实快照/校准公开Build/Verify及最终所有权修订重放均同SHA`769cf6ff...`：完整gift410/talk43不变，10项已证明，Lewis实际3连接器/08:40接触，8个严格成对证据。20项未比较仍保留（17无匹配当前目标窗口、2超日预算、George08:14与Lewis08:40为真实权衡）；默认完整排序标签仍false、无NPC/训练/覆盖晋升。最终完整Core1824/Backend597零跳过（含71专项及释放文档后的所有权断言）、242脚本AST与原EVD-338通过，最终CI见本PR收口。[真实结果与接续](friendship-route-contact-budget.md)。
- **唯一下一步：** 新Friendship消费端增加显式“独立成对证据”准入/控制模式，复用现builder/Verify/原编译；默认完整排序拒绝和全部安全门保留，不伪造George等20项负标签。原#394训练合同/实际trainer只消费显式pairs，并不要求每个pair反向证明完整排序；不改共享trainer或Teacher政策。再E副本同run 5/10真实人数阈值、原轨迹/provider/corpus/覆盖三分区。此模式尚未实现，不冒充8个证据已可正式训练；全局Teacher责任保持。无新游戏/worker/API/服务器/模型训练/清理，不提交原始数据。

## 2026-11-06 Friendship全量离线源接续

- #456经CI `42122800708`全部38步接受，合入`c53b6a0d`、分支清理；真实移动校准不重采。唯一primary仍`raise_friendships`，整项`9/19、9/19、9/19、7/11 roots`及formal=false不变。
- 新比较层显式选择原evaluator的完整离线源，旧两个构造函数仍默认64，原桥/Teacher/绑定/编译执行不改。v2审计保留源实例数量、上限及全部不可用重复记录；有可用或可能可用的重复ID仍阻塞。旧v1只保留诊断，必须从原输入重建，不能改写原文件冒充完整。
- 实际原始快照/校准只读重放：gift410/排名0、talk43实例/42 ID/排名30，齐先生两份不可交互重复均保留；完整分母true。Build/Verify同SHA`d2d7323b...`，无NPC动作或训练样本。53专项、完整Core1806/Backend597零跳过、242脚本AST及原EVD-338通过，最终CI见本PR收口。[合同与真实结果](friendship-complete-intake.md)。
- **唯一下一步：** 新比较层对接原校准接触前沿/完整跨图预算及原单连接器执行接续。实际旧Teacher已选Lewis、3连接器/08:40，但新比较层缺跨图几何适配，仍正确blocked；不能拿首段当全程、捏造远端站位或改旧执行器迎合新消费者。先重放同一真实来源，再原生5/10阈值与原数据/覆盖，不重开校准。65项专项另保留旧即时编译匹配窗口的拒绝，不隐去未比较项。无游戏/worker/API/服务器/模型训练/清理，不提交原始数据。

## 2026-11-06 Friendship真实校准与原暂停契约接续

- #455最终头`76054c4d`经CI `42117366815`全部38步接受，合入`a699a3e9`、分支清理。比较/派发消费端不重做；唯一primary仍为`raise_friendships`，`9/19、9/19、9/19、7/11 roots`及formal=false保持。
- 修正校准读取器将解码DateTime先转字符串而丢失小数秒的实证问题；864.1976ms诊断由0恢复原值。复用原Core暂停证书谓词，新增只读委托/API入口，原桥、执行器、暂停释放和1/2时间上界不改。首轮a在采样前拒绝，真实b轮3段66格及门/步入传送均通过，源指纹`4164c3ae...`退出后仍一致；双MOD隔离确认有效，不误改原SMAPI环境变量机制。
- 原加载器接受真实校准SHA`e0c0e82f...`；完整9文件13.25MiB已复制核验到仓库外。Core1803/Backend597、35专项、显式E构建、AST/EVD-338/差异检查通过、零跳过，最终CI见本PR收口。动作更新频率约54.9–55.8Hz只是诊断，不是画面FPS验收。游戏已退出，无worker/API、服务器或训练，不提交原始数据/local-data。
- **下一步按真实证据先行：** 实际快照40原生实例/1达标，送礼源触顶64、talk源42/排名30，原完整分母门正确阻塞，无NPC动作或伪造偏好。先给新离线比较增加完整源入口、保留旧运行时默认64，再复用已保留实际快照/校准重放，随后同run真实5/10人数阈值及原轨迹/corpus/覆盖。不删候选、不清库存绕门，不重采已通过校准。[来源、溯源和退出边界](friendship-real-calibration.md)。

## 2026-11-06 Friendship独立比较及原队列消费端

- #454经CI `42108251212`全部38步接受，合入`efbbe098`，分支清理；当前缺口读取已完成，不重做。唯一primary仍为`raise_friendships`，接受集合`9/19 executable、9/19 split-complete、9/19 ready、7/11 roots`和formal=false保持。
- 新消费层保留原社交Teacher的有序行程/控制提案，完整审计talk/gift及中性化决策上下文；通过原编译器和校准当前接触窗口后，仅在新层补即时许可证据，再走原严格绑定。旧null时间许可、候选上限和新派发GUID都不反改，不新增执行器。旧运行接口的64候选饱和被上游明确阻塞，不冒充全分母；相同方案、不可行、送礼/路线等真实资源权衡不强造负偏好。
- 50专项检查（25新、25原社交回归）及完整Core1800/Backend597通过、零跳过；AST/EVD-338/差异检查通过。公开最终b版build/Verify同SHA`c17ebeb3...`，原编译生成pending的移动＋社交交互队列；这些全部是**合成协议夹具，不是真实游戏证据或训练样本**。原ranker去重前另审源ID重复，a版保留诊断。最终CI见本PR收口；代码/本地原生数据和旧执行链隔离不变。
- **唯一下一步：** 新公开比较/编译入口接原E隔离社交runner取得同run真实5/10人数阈值，再原轨迹/数据集/corpus/方法及coverage分区。不得拿旧未适配label入口当新比较，不得将合成校准用作真实采样。跨种权衡和触顶时的全量候选消费边界保留为明确未完项，不缩减全量目标。[完整合同与接续](friendship-independent-comparison.md)。无worker/API、服务器或产品模型训练。

## 2026-11-06 Friendship当前权威缺口接入

- #453最终头`a0c6a138`经CI `42104419946`全部38步接受，合入`712482ea`，对应分支已清理；Skills局部三分区已收口，不重开。接受集合保持`9/19 executable、9/19 split-complete、9/19 ready、7/11 roots`，formal=false。
- 唯一primary为`raise_friendships`。实时核对E游戏DLL及新反编译Utility/Farmer，新增只读typed当前缺口与公开Bootstrap入口；直接消费原桥完整原生遍历，不改桥、旧社交Teacher/行程/绑定/编译执行和训练规则。1975..999999原生边界、5/10人数分别核验；无记录保留null，不可读阻塞，重名实例保留评分计数并标记目标歧义。45项针对性检查及完整Core1750/Backend597通过、零跳过，AST/EVD-338/差异检查通过，最终CI/发布以本PR收口为准。
- 复用原保留快照，无新游戏；公开重建两次同SHA`6404205b...`，40原生实例/0达标/37无记录/齐先生两实例，源SHA未变。仅当前事实，不是Teacher比较或native动作完成，不晋升评分项。**唯一下一步：** 复用原社交Teacher/有序行程补独立可重放比较，再原队列同run原生人数阈值和原coverage三分区；不另造执行器，不把全新档责任反挂L1。[完整合同和来源](friendship-local-intake.md)。无worker/API、服务器、训练或数据清理。

## 2026-11-06 Skills三分区局部验收收口

- #452最终头`49ed4f81`经CI `42095890029`全部38步接受，合入`4098b743`、分支清理完成。本轮复用原动作/Teacher/corpus/局部视图/coverage，补四个E后台静音隔离来源，各实际5XP、原生等级`14→15`或`24→25`，真实分区validation/test。原train两行不重采，源档SHA`68ddc8db...`、游戏DLL SHA`7f1e5b8e...`保持，进程退出。
- 首次validation a因原始书籍随机标记不同而在收取前被正确拒绝；已实时反编译并用原生HashUtility核对，只调整明确声明的对照夹具位置，不改Teacher/桥/执行器。六行最终corpus SHA`a176d5f0...`，两项比较/native各自三分区齐全；原覆盖两项ready、无blocker，对账SHA`f918dae5...`无Skills局部open work。
- 同时纠正新消费端的输入边界：独立重置样本不能走旧连续存档的跨日期回报推断。核对旧`b326bf8c`及聊天JSON后，新增显式独立模式，复用旧backfiller逐行保持未知回报，默认规则及四参数二进制/default调用保留。最终原PolicyDataset接收6行、0拒绝、每分区2行、全部长期回报pending，manifest SHA`b3da7314...`；首份默认模式输出仅诊断、不训练。六项新检查、最终Core1705/Backend597零跳过，原生数据不重采、不提交local-data。
- **发布及计数：** 以本PR最终CI/收口为准。与原接受集合取并集后为`9/19 executable、9/19 split-complete、9/19 CoverageGateReady、7/11 roots`；这是评分条件局部闭环，不是基本动作数量或完整新档Teacher。formal=false，完整五技能获取/全局时间资源/Teacher21分与Student仍未证明。[本项验收、完整来源与诊断](skills-local-acceptance.md)，[独立样本输入合同](policy-independent-decisions.md)。
- **唯一下一步：** 收口后切`raise_friendships`，复用原当前社交Teacher/有序行程和talk/gift编译执行；先当前权威1975点/人数缺口，再独立比较、同run原生与原覆盖。不重开Skills/Rusty/Master已接受门，不重写社交执行器。其余婚房、全馆、Money留队列，R2与全量目标不缩减；本轮无worker/API、服务器或产品模型训练。

## 2026-11-06 Skills局部方法责任已对齐

- #451最终头`313e22c9`经CI `42091079266`全部38步接受，合入`8d497d54`，对应分支已清理。corpus/原source登记已完成，本轮没有重做或重采。已沿旧提交`55dc054f`找到2026-09-08聊天JSON及原收敛计划上下文：旧图承担完整Teacher责任，不将其当bug删除。
- 新公开build/Verify只生成仓库外的运行局部依赖视图，复用原corpus Verify及原方法图/coverage；原canonical图和其他目标不改。视图保留原全新档五技能XP/行程/体力/跨目标budget与原递推规则，另绑定两个阈值实际来源的完整frame/队列/nonce和当前合法编译，明确不是前期获取或未来供应证明。五技能10级、原15/25规则及所有获取节点保留，当前Teacher仅声明已实现的同效果支配策略，不冒充全局最优。
- 最终b版视图将所选动作原OptionRegistry字段完整合并入证书，公开重建同SHA`4d331504...`；五类scope/覆盖反例拒绝、输入未变。原coverage入口额外拒绝手改scope和错配corpus，未增加一次native深验；原canonical图仍pending。真正局部视图下两项method为`executable_frontier`，Teacher/native仍各train、ready=false；其他17项覆盖逐项完全相同，对账剩余仅validation/test比较及原生分区，不再有方法图或adapter blocker。
- **唯一下一步：** 本切片回归/CI收口后，只补15/25各自真正缺失的原policy validation/test来源，先核对日期分区，再顺序运行E隔离后台静音采样；原train与旧方法/执行器不重做。沿原Teacher行/corpus/局部视图及原coverage/reconciliation重建到两项局部ready，之后才晋升整项发布面板。当前冻结整项面板`7/19、7/19、7/19、6/11`及formal=false不变，局部method进展不冒充完整Skills/Teacher或正式开训。
- 18新检查、最终完整Core1699/Backend597通过，零跳过；公开来源/入口反例、全脚本AST及EVD-338检查点通过，最终发布见本PR收口。无新游戏、worker/API、服务器或产品训练，不提交local-data。详细来源和后续边界见[局部方法视图](skills-local-intake.md#conditional-local-method-view)。

## 2026-11-06 Skills criterion corpus与原覆盖登记

- #450最终头`927df128`经CI `42085984353`全部38步接受，合入`8f6a36fd`，对应分支已清理；原Teacher行接入已完成，不重做原生15/25样本。本轮只接Step5的typed corpus及原coverage/reconciliation source，不修改旧执行器、共享准入或方法完成规则。
- 两条最终b版Teacher行由原行Verify重放完整native链，按原始run/save/决策/trajectory/nonce/queue-item去重，绑定当前完整19 criterion/21分权威索引。corpus公开build/Verify逐字节同SHA`2248b6e4...`；15与25各自保存Teacher/native的原policy分区交集，不做方法级借分。五类公开篡改/覆盖拒绝，仅15来源经原19项覆盖入口确认25无任何比较/终态记功，源文件未变。
- 原覆盖与对账入口已识别`skill_level_threshold_corpus`：两项分别只有`train`、方法仍`pending_dependency_expansion`、ready=false；对账不再缺source adapter，剩下局部方法责任与真正缺失的validation/test。报告以当前权威索引重建，不消费旧SHA的缓存frontier。已有fresh-save五技能总预算没有清除或冒充完成，L3/L4也不能反向无限扩大L1。
- **唯一下一步：** 先对齐原Skills递推图的局部有条件方法及真实共享能力前置，保留全新档五技能XP/行程/体力/跨目标预算为独立全局责任；随后仅补15/25各自缺失的原policy validation/test，并从原入口重建验收。当前源登记不等于方法executable、三分区完成或Teacher整体验收。冻结`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`及formal=false不变。
- 本地完整Core1681/Backend597通过、零跳过，17新检查；Bootstrap显式E构建零警告错误、公开corpus探针及EVD-338检查点通过。发布以最终CI/收口记录为准；详细工件和边界见[Skills criterion corpus](skills-local-intake.md#criterion-corpus-and-original-coverage)。本轮无新游戏/worker/API/服务器/产品模型训练，不提交local-data或原始证据。

## 2026-11-06 Skills已接原Teacher轨迹及混合入库

- 最终本地完整Core1664/Backend597零跳过，30新检查覆盖provider路由、旧schema保护、完整分母和Skills输入；两CLI构建零警告错误，原映射/预检、AST和EVD-338通过。单测仅用微型合成夹具验证原trainer，不是产品模型训练；发布仍以本PR最终CI/收口评论为准。

- #449最终头`18d3ff90`经CI `42076554119`接受，合入`1681ad9b`，本轮旧分支已清理。Step4两条真实阈值来源不重采；本轮离线重建完整候选上下文、原队列回执及同原生动作/nonce/facts，生成两条原`policy_decision_trajectory.v2`监督行，最终b版公开Verify分别同SHA`bbc5d3da...`/`ae0d1b9e...`。
- 复用原数据集/训练器和原分区；增加显式Skills监督来源子类型与精确ID/version路由，不是假造CC哈希或另建训练格式。旧Collection八哈希、原独立重放及单provider注入语义保留，实际旧监督行与两条Skills行经同一公开PolicyDataset CLI接受`3条、0拒绝、仅train`；原loop无独立监督反馈被正确拒绝。九类公开篡改拒绝、源输入未变；无新游戏/worker/API/服务器/产品模型训练，原始数据及local-data不提交。
- 实际核验发现通用紧凑特征只有总等级，故本新消费端补显式Skills投影，Teacher行、Backend推理及loop反馈共用五技能等级/XP/缺口/cap与15/25缺口；未知或跨前态拒绝，旧目标特征字节不变。旧泛化投影及模型标准化未重写；a版只作诊断，同一native来源离线重建b版。完整回归以最终记录为准，旧Backend身份不匹配断言曾抓到新路由分类回退，现仅修新路由，保留旧测试。
- 本切片最终回归/CI以PR收口评论为准；详细共享边界与来源见[Skills原Policy行](skills-local-intake.md#original-policy-teacher-rows)。**唯一下一步仍同Step5：** typed criterion corpus、原coverage/source登记与原局部方法责任，之后只补真正缺失分区。两条仍是day118/train，不晋升整项；冻结`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`及formal=false不变，不把旧Step4的false标记改写成更广声明。

## 2026-11-06 Skills原生15/25阈值来源已贯通

- #448最终头`91ee64bf`经CI `42067595027`接受并合入`99d352ca`、对应分支已清理；下方Step3待CI已完成，不再重做。唯一primary仍`raise_skill_levels`，冻结并集`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`与formal=false不变。
- 新消费端先独立比较两个同效果蟹笼，再调用原DailyPlan/ActionQueue编译和原单次预编译执行入口；新两次E后台静音运行各真正增加5XP，钓鱼`3295/5→3300/6`、评分`14→15`，以及`14995/9→15000/10`、评分`24→25`，均`128→155tick`。前置等级/XP、现成蟹笼和日期明确声明，未注入测量奖励，不冒充前期获取/自然生产。源档目录SHA`68ddc8db...`不变，进程退出。
- 新公开native receipt build/Verify复用原强reader/请求gzip/nonce及原语义比较器，核验完整原编译项、完整解包前态、五技能真实增量和评分阈值。原始JSON与压缩清单物理哈希分别保留，不改旧哈希/执行门。独立重建分别同SHA`6c86c4da...`/`9eaffbcf...`，六类公开CLI篡改均拒绝、原输入未变。首次新调用漏参数/误比物理哈希已修，只离线重建成功15动作，不重采；25新鲜度拦截后显式重建，不绕旧门。
- 完整Core1634/Backend597零跳过，原Collection源码测试只更新分支定位，保留不筛窄候选断言；发布结果以本PR最终CI/收口评论为准，不为发布重复采证。两次均day118，还不是typed Teacher行/corpus、方法executable或三分区整项完成，不晋升计数。来源/诊断见[Skills局部接续](skills-local-intake.md#same-run-native-threshold-sources)。无worker/API/服务器/模型训练，不提交local-data或原始产物。**唯一下一步：** 收口后沿Step5接原Teacher行/criterion corpus与coverage，复核原局部方法责任，再补真正缺失的policy分区；不重做动作或重采成功阈值。

## 2026-11-06 Skills独立当前比较已接线

- #447最终头`4b3c7400`经CI `42059282002`和无发现审查接受，合入`bb336431`、分支已清理。下方当前XP缺口消费端的待CI已完成，不重新实现或重采。唯一primary仍`raise_skill_levels`；冻结并集`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`、formal=false不变。
- 本轮新增当前Skills comparison公开build/Verify：由同一前态重新调用原registry/evaluator/ranker，清除learner信号，复用原XP绑定校验与原编译。16个策略族逐项保留原可用性/缺字段原因，5个编译器所有项不成为策略标签。蟹笼只在新消费端适配原`expected_skill_*`，没有改旧候选/桥/执行语义；通用标量、结构化及双技能格式经原guard后只做类型化读取。
- 原ranker已调用时间轴，但旧本地候选可保留null许可；不是旧逻辑错误。新显式许可只由同位置、无门/等待/续程、原完整plan/queue pending及预算证明产生；false、跨图、未知窗口不兜底。比较保留完整native参数/未知副作用，仅剔除明确几何和本消费端时间成本字段；互有取舍、随机界重叠、同分、物理别名不造标签。按同效果分组，只保留支配见证/最终所选比较，避免跨族二次方输出。
- 保留真实前态`master-angler-terminal-train-20261105-a`的只读重放得到两项真实可编译候选，5XP/同产物，30tick/1分钟优于210tick/5分钟。公开Verify关闭缓存独立重建同SHA`016d4cf2...`；仍只是事后读取旧前态的独立比较，不是新决策时序、Skills原生升级或可入库训练行。48新检查、完整Core1612/Backend594零跳过，CLI篡改formal/覆盖输入均拒绝；当前E DLL SHA不变，无新游戏/worker/API/服务器/训练，不提交local-data。新切片尚待最终CI/合入。
- **唯一下一步：** 收口本切片后按[Skills局部接续](skills-local-intake.md#independent-current-comparison)第4步，在新的E隔离运行中先生成本比较、再调用原编译/执行取得精确XP及原生15/25阈值；局部边界前置须声明，不把旧Master执行拼成新Skills决策，不重复Step2/3、不增加另一套动作。之后才做同run join/corpus、原policy三分区和原覆盖入口，计数不能提前晋升。

## 2026-11-05 Master Angler已合入，接续Skills局部来源

- PR #446最终头`ad3e6b5b`的CI `42054598280`全部通过，审查无发现，合并为`56a133dc`；已核实main祖先关系并清理本轮本地/远端分支。三分区终态、完整回归和冻结并集现已接受，不再待CI。游戏/后端已退出，5129/8765/8767无监听。`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`，formal=false。
- 五个未关root只读复核后，唯一primary切为`raise_skill_levels`。五类XP候选、方向绑定、原编译及原生XP验证已存在；先复用这些链路，补基础等级/XP缺口与原生阈值的类型化消费，再接独立比较、同run终态和原覆盖入口。当前没有Skills新增来源、局部完成或计数晋升。Friendship/婚房/全馆/Money留队列，不并行新建。
- 已从当前E游戏重新反编译`Farmer`/`Utility`并与wiki复核原生XP阶梯、立即基础升级、15/25评分及精通分流。原桥`99cdba2b`的阈值读取符合原生；原始July对话未在留存JSON中找到，不冒称恢复上下文或更改旧规则。反编译及哈希仅在仓库外，见[Skills局部接续](skills-local-intake.md)。
- 已实现单一C# `CurrentSkillExperienceDeficitBuilder`及原Bootstrap公开CLI，复用原snapshot reader/hash，不改旧桥/绑定/编译/执行：严格核对六行原字段，输出五技能基础等级、下一等级/满级XP缺口和15/25所缺基础等级；Luck/增益/精通不冒充进度。27项检查、完整Core1564/Backend594零跳过；旧真实压缩后态只读重放为钓鱼5XP、总缺74995，独立重建同SHA`cf082f34...`。重序列化诊断副本因哈希不符正确拒绝，原来源未改；CLI不能覆盖输入。该结果仅当前状态，Teacher标签/native outcome均false，未开新游戏/训练。
- **唯一下一步：** 本切片CI/合入后，沿[Skills局部接续](skills-local-intake.md)第3步将当前缺口连接原合法正XP候选/时间资源合同，产生独立比较，再接同run原生阈值及原coverage；不另写动作、不把完整新档五技能预算提前挂到L1。无worker/API，不提交local-data。下方Master“待CI/唯一接续”均为历史前态，以本段为准。

## 2026-11-05 Master Angler局部三分区终态收口（发布前记录，已由上段收口）

- #445最终CI`42042195162`已通过并合并`c2c0cf3c`、分支已清理；旧杆钓/蟹笼进度不重采。本轮新增C#隔离副本准备入口，仅声明完整权威71种前置、缺716及第二年春日期，拒绝既得26/错版本/非同字节副本/越界或重解析路径。原文件只读，原子替换副本；实际硬链接检查保住源字节，报告路径也不能覆盖源档或已有元数据。71种及24/25属于前置，不伪称真实取得；26绝不注入。
- 三个新的后台静音原生运行分别在春7/21/24日，原Teacher独立比较30/210tick两笼，原编译/执行实际收取最后716并由游戏获得26。原receipt/PolicyDataset/精确join独立通过，各1条严格比较、0拒绝，原policy分区依次train/validation/test；源档目录SHA`68ddc8db...`不变，进程退出。非新档/连续过夜证明，未训练模型。
- 合并已留两条进度来源后，5行corpus/独立重放同SHA`78e334c1...`，3行真正成就终态；原coverage方法executable、双通道三分区、ready=true。补上我们新来源漏登记的对账表项后，本项`open_work_kinds=[]`，最终reconciliation SHA`f662a72c...`；未改旧分类/Teacher/执行器/BFS/碰撞。旧`next_actions`未选备选通道提示不属于本项blocker，不能据此重开。
- 新增17准备安全检查与1登记检查、完整game-free Core1537/Backend594、原轻量映射与EVD-338检查点通过，零跳过；公开CLI拒绝覆盖源文件/已有元数据且不改试验源。仅本切片最终CI/合入待收口。最终CI后，与冻结集合取并集为`7/19 executable、7/19 split-complete、7/19 CoverageGateReady、6/11 roots`；Master原已在split集合，不重复加数。formal=false，无worker/API/服务器操作，不提交local-data。
- **唯一接续：** 先完成本头回归/提交/CI并合入清理分支，再只读比较五个未关root的真实局部成本，按R2选一个并更新施工顺序。Master局部不再重采或追加全局前置，L3/L4和完整Teacher/Student新档21分仍保留。[当前局部验收、全部工件与边界](master-angler-local-acceptance.md)。

## 2026-11-05 蟹笼真实比较与原生收取已接入corpus

- #444最终CI`42034440832`成功，已合并`42a9edd4`并清理分支，下方待CI已完成。本轮仅扩展原隔离采样消费端，调用旧合法水格/ready-pot夹具和旧收取执行器；原杆钓随机域校验保留，未修改旧Teacher、站位/BFS、碰撞或透明桥。helper分离采样投影、夹具与原公开native Verify，不复制物理验证器。
- 最终隐藏静音运行`master-angler-crab-pot-teacher-20261105-f`在合法水格`39,51`、`39,56`准备716待收取笼。原Teacher对预计30/210tick两项产生1对严格比较并选择前者，原生收取/caughtFish使收集false→true；原公开来源build/Verify通过完整72分母和原参数/journal/前后态。源档SHA`68ddc8db...`不变，游戏/后端退出，无模型训练。准备不证明制作、放置、上饵或自然隔夜生产。
- 原receipt/PolicyDataset独立重放接受1条train、0拒绝；精确join重放同SHA`4b1955ba6473471a8854090cc855ea848aa4ee49d954e2fe52e042dca1334f3b`。与旧708杆钓组成2行corpus，同SHA`41ae160448ed60229483c8b351179e9d1a064dd35d283dd908dc72a25d8af2c5`，仍同一原`ProofFarm:1:spring:7`组，不拆分区，不声称同局连续取得两种鱼。原coverage仍方法executable、比较`[train]`、终态`[]`、ready=false。数据仅在`I:\StardewAITraining\master-angler-local-20261105\crab-pot-joined-source`及原ignored运行目录。
- A单候选、B/C另一站位BFS不连通、D/E原Teacher同分；诊断/物理来源保留，不入监督corpus，不改原fallback或造负偏好。最终位置按留存网格和原BFS核实，勿重采这些失败场景。当前E游戏实时反编译`CrabPot.checkForAction`及wiki已复核。11事件投影、5预检拒绝、原映射、51来源/join/corpus及38执行入口/profile聚焦通过；公开入口拒绝A导出监督行且无成功行文件。game-dependent校准守卫的game-free过滤未匹配，不计PASS。本头发布以最终CI/合并记录为准，勿凭历史待CI状态重开验证。
- **唯一接续：** 保留已成功杆钓/蟹笼，补71/72明确局部边界下的真实成就26终态及原policy validation/test，维持独立比较/同run原生连接；71种前置不能冒充真实取得，R2与冻结`6/19、7/19、6/19、5/11`及formal=false不变。无worker/API/服务器更新，不提交local-data。[来源与诊断](master-angler-local-native-join.md#retained-trap-source-and-diagnostics)。

## 2026-11-05 Master Angler corpus已接原覆盖入口

- 本轮接续上轮精确join，新增可公开build/Verify的criterion corpus，只复用原独立比较、数据集结构、原生catch及72分母验证器。原policy分区保持，按决策/trajectory/nonce/queue-item去重，混用authority或改名复制拒绝；新source kind只接原coverage聚合，不新增Teacher、编译器、执行器或覆盖计算器。
- **真实保留来源为1条train、6对严格比较、0条成就终态。** corpus构建/独立重放同SHA`4909b0098f906c6d09c8378d8bbb503cf266f2052f54e38662bf05bac494b95d`。单鱼false→true只能计collection progress，只有同一原生catch的71/72→72/72且achievement26 absent→present才计criterion native outcome；当前E游戏1.6.15的`Farmer.caughtFish`/`Stats.checkForFishingAchievements`实时反编译及wiki已交叉核对，透明桥已有全部所需字段，无须增加扫描。
- 原R2前置proof经已有构建器重建run-local依赖闭合，再交原公开coverage CLI，得到本方法`executable_frontier`、比较`[train]`、native outcome`[]`、split/ready=false；exit2为预期未全覆盖，不是实现失败。不改canonical依赖，不把前置声明闭合当真实成就，不用本项单来源报告覆盖全局冻结并集。公开反例拒绝同原生决策改名重复入库和普通进度篡改为终态，均不写成功工件。
- 13项新增合成分类/去重检查、完整game-free Core1519、Backend594及EVD-338检查点全部通过，零跳过；原生数据未改，不提交local-data。本轮未开游戏/模型/worker/API或更新服务器，代码待本切片最终CI收口。
- **唯一接续：** 用原隔离collector、原collection Teacher和原执行器补真正缺失的蟹笼来源、成就26终态及原policy validation/test；可声明71/72局部边界，但不能声称此前71种已真实获取。保留72/14、代表抽样及R2四层责任，不为每鱼新增长跑。多队列续接来源链仍须独立绑定，不反改旧聚合回执来迎合消费者；本corpus不证明该缺口已解决。`6/19、7/19、6/19、5/11`及formal=false保持。[本轮合同](master-angler-local-native-join.md#criterion-corpus-and-original-coverage)。

## 2026-11-05 Master Angler精确决策typed join已重建

- `MasterAnglerTeacherNativeJoinBuilder`及公开build/Verify已接原数据集结构校验、`PolicyPairwiseSupervisionAdmission`/原独立replayer和原精确native catch Verify。只读`PolicyTrajectoryDatasetBuilder.ValidateEnvelope`仅转发原内部校验器，旧数据集/训练器调用和规则未改变；未新增Teacher、编译器、执行器或放宽旧聚合回执。
- 消费上轮已保留的真实监督行和708原生证明，核对原authority/降级映射、完整前后态来源、run/save、原编译队列全部语义、选中candidate/queue/item、实际tick跨度、目标鱼和原policy分区。公开build与最终Verify同SHA`8a4dccb27884008dad528db7df4040d9709361e1814a2c9953e0272d397a155a`，仍是原`ProofFarm:1:spring:7`的一条train、6对严格比较；没有重开游戏或生成新训练样本。
- 29项新增协议/结构入口检查及完整game-free Core1506、Backend594均通过，零跳过；4个真实来源CLI反例拒绝跨run有效证明拼接、无独立比较的原生观测、篡改formal声明和不合法结构版本，且不写成功工件。原始观测未改，local-data不提交。
- **唯一接续：** 将这些可重建join接入原`GoalMethodTeacherCoverageBuilder`的来源聚合/去重和criterion corpus，再补真正缺失的蟹笼及validation/test；不复制比较/执行系统，不重采已成功的精确杆钓决策。对多队列续接保留完整来源链责任，不把旧末端queue冒充初始Teacher queue，不把随机未命中造为负偏好；当前精确join不宣称此边界或整项已完成。原分栏`6/19、7/19、6/19、5/11`与formal=false保持不变，无worker/API/服务器操作。[精确join合同](master-angler-local-native-join.md#exact-decision-typed-join)。

## 2026-11-05 独立Teacher到真实杆钓单决策已重放

- 唯一primary仍为`complete_master_angler`。新增显式`--standalone-collection-teacher`仅接原`TryBuildStageOneCollectionTeacherQueueAsync`，要求禁训、原生反馈、现存隔离目录与四份authority，拒绝learner checkpoint、候选过滤/钉选和全局模式混接。`cfcac4674`当时为作物结束后切入collection控制，旧三个激活条件及同分控制/标签分离均保留；留存JSON仅找到后续git-log引用，未臆称找回原始对话。
- 新`collection_teacher` profile复用原fishing域和既有日期静态路线读取器，补足Teacher真实消费的`locations.social_route_date_evidence`；不扩大旧fishing，不启用NPC日程全表。采样入口也能识别原续接队列，未修改旧聚合回执或执行器；无游戏映射测试覆盖续接/稀疏序号及未知/重复queue拒绝。
- 最终后台静音运行`master-angler-independent-teacher-20261105-final`保留72鱼种，原Teacher产生6对严格比较，按最早关闭的窗口选择`(O)708`，同run原生实际钓获并完成收集false→true。原receipt构建器为ready，原`CollectionTeacherSupervisionReplayVerifier`经PolicyDataset公开CLI重放得到**1条train、0拒绝、validation/test=0**，并未训练模型。原生来源公开build/Verify同SHA`7e04e8bc37b6ac78dca641aa7f7d40865d194a1633293dbd9afd7276ed29f01b`；Teacher与原生前态来源SHA相同。源存档哈希不变，游戏/后端均退出。
- 早期A运行因窄档缺日期路线而没有执行；B有独立比较和两次原生捕获131，但旧聚合回执记录续接queue，不能冒充原初始queue的精确Teacher终态，原receipt正确拒绝。保留诊断和随机未命中，不改旧日志、不造负偏好、不改共享聚合格式。完整Backend594通过（零跳过），Bridge/PolicyDataset显式E构建零警告错误；一次Core旧DLL过滤未匹配测试，不计PASS。
- **唯一接续：** 从已保留的最终监督行和精确原生来源做机器可重建的同run typed join/corpus，复用原reader、比较/receipt/replayer；核对完整来源、状态/tick、actor、queue/item/nonce及原决策分区后，再补真正缺失的蟹笼/validation/test，不重采本单次杆钓成功或重开接受门。已有两个独立验证工件不冒充统一corpus已完成。全局`6/19、7/19、6/19、5/11`及formal=false不变，无worker/API、无服务器部署、不提交local-data。[本轮合同与证据](master-angler-local-native-join.md#standalone-independent-teacher-source)。

## 2026-11-05 已恢复Master Angler：真实杆钓来源落盘

- 用户已要求继续开发，下方共享修补的“主线暂停/等待指令”仅记录上一轮结束点。唯一primary仍是`complete_master_angler`；#440来源入口已CI通过并合入`9bc2ea87`，不再是待实现/待CI任务。
- 原钓鱼日计划采样入口新增可选复制存档、双MOD隔离、现有location fixture和显式E路径/已构建DLL入口；保留原机械编译和原生钓鱼，不新增执行器、不使用模型训练。两次隐藏静音运行都在第7日取得真实新鱼：初次`(O)131`，最终版单次`(O)147`。完整72分母、原始请求/queue/nonce/精确前后态经公开build和Verify通过；最终回执同SHA`39b7bd5e8368e803ed1680652d4cf62a69b3b1a2f41b610b9941551ee8188fa4`。原始数据仅本地保留，16项钓鱼/领奖回归零失败/跳过。
- 最终版隔离审计确认源档哈希不变，后台游戏/后端均已退出。初次后续背包领奖阻塞和失效候选保留，不算成功。两次运行同一决策日，不能拆分成不同corpus分区；夹具只证明准备后真实捕鱼，不证明旅行、购竿或全流程。
- **唯一接续：** 复用原collection Teacher/preference/receipt及`CollectionTeacherSupervisionReplayVerifier`，将真实动作来源和独立比较做同run因果连接，再补真正缺失的蟹笼来源/决策分区。不重采已有杆钓协议证明，不伪造Teacher偏好，不重开接受门。计数`6/19、7/19、6/19、5/11`与formal=false不变，无worker/API或服务器更新。[完整接续合同](master-angler-local-native-join.md#retained-real-rod-source)。

## 2026-11-05 共享执行器修补收尾（历史暂停点，现已恢复主线）

- 修改旧执行器之前，必须先查引入提交与对应对话 JSON，确认原始故障和调用合同；新模块优先适配旧公共链路，不能仅因新增验收标准而反改旧实现。本轮已按 `989fa5f7 / d33e3c72 / deee46e9 / 48c34fb6` 核对原意，保留职业、资源归属、晶球和防重复派发等必要保护。
- 已修三个实证共享影响：原生过夜完成后不再被后续工作证明占用；搬迁读取真实路径距离，不再把动态绕路预算反推成距离，时间预算仍保守计入；隐藏禁绘在原生 FarmEvent 期间让原生 draw 正常推进，避免齐先生飞机事件卡住。没有第二套执行器或假事件完成。
- 验证：Core 专项225、Backend专项313、完整 game-free Core1477均通过，零跳过；E路径运行时构建零警告/错误。双MOD后台静音原生回归通过职业/保存/次日工作及无规划标记的连续两夜，空闲暂停恢复，旧未满足证明保留blocked，源存档未变。早期环境/夹具诊断不算PASS，原始数据不提交。详见[本轮溯源、证据与限制](shared-executor-compatibility-audit-20261105.md)。
- 本轮仅审计修补，不恢复 Master Angler 原生采样或模型训练，不重开已接受的 #381 目视门。原primary、`6/19、7/19、6/19、5/11`与formal=false不变；无worker/API调用、无服务器更新。等待用户继续指令。

## 当前切片与接续（Master Angler）

- **2026-11-05 精确捕鱼来源入口已实现并回归：** `MasterAnglerNativeCatchReceiptBuilder` 的build/Verify CLI只接受杆钓/蟹笼原生新收集，原72分母、版本、完整前后态和既有收集单调性必须成立；杆钓核对实际鱼种/原随机域，蟹笼核对真实caughtFish参数，不把制作/领奖恢复计捕获。请求/队列/快照绑定从原Rusty实现抽为同一reader，未复制校验器；Rusty仍保留自己的动作白名单、跨日和连续性。18项新合成检查、完整Core1397、Backend565均通过，原真实三分区Rusty公开Verify仍同SHA`4ae5a1ba...`。#439已CI通过并合入`eb275df9`，下条该轮待提交/CI已过期。当前来源入口尚待本头提交/CI；不是新增真实捕鱼验收，Teacher/native join和corpus未完成，计数及formal=false不变。**唯一接续：** 收口当前代码后，以原fishing profile/隔离E档后台静音取得真正缺失的原生source，再接原collection Teacher/receipt/replayer；禁止为来源短测误启模型训练、补写旧观测或新增第二套执行器。详见[精确来源合同](master-angler-local-native-join.md#exact-native-catch-source)。无worker/API/新游戏，local-data不提交。

- **2026-11-05 Master Angler来源修补已验证（当前primary不变）：** 已复现原收集回执只验目标鱼false→true、误收7种全量后态异常。现终端回执复用原前沿的同一72鱼种读取器，按原权威inventory检查前/后态并拒绝既有收集倒退；不复制校验器，不改执行器/采集奖励/旧数据或其他collection分支。9项聚焦、完整game-free Core1379、Backend565、原独立监督CLI均通过，零跳过；EVD-338不变。旧item-grab样本只是领奖恢复，前态无收集字段，保留原作用而不冒充新捕获；现有fishing profile已有所需字段，不加全量逐帧扫描。代码提交/CI收口后，下一实质仍是复用原collection Teacher/receipt/replayer建立真实比较与原生来源连接，保留72/14及R2分层，不换root。尚无本项新native验收或覆盖晋升，`6/19、7/19、6/19、5/11`与formal=false不变；无worker/API/新游戏，local-data不提交。[合同](master-angler-local-native-join.md)。

- **2026-11-05 R2接续选定Master Angler（本条优先）：** Rusty Key已由#437/#438合入收口并向用户汇报，不再占WIP。按R2重新核对六个未闭合root，现唯一primary为`complete_master_angler`：原姜岛能力证书公开重建仍同SHA`6031276d...`；用当前含Rusty闭合的依赖图重建72鱼种/14前置族audit与proof仍ready，proof SHA`5d78acf7...`，无新游戏/全局核桃日程。相比Skills缺typed source adapter、Friendship缺未来日期接触证据、Marriage组合链、Museum异构来源概率和Money18格，本项已确认局部前置复用成本最低，余下实质是独立比较/真实原生来源的接线与分区核实，不把旧R1全新档长跑恢复为前置。找到的旧coverage请求明确引用self-test fixture，不能据此新增真实native完成主张；保留旧分栏计数，当前不抬Ready。**下一步：** 只读审查既有真实钓鱼/蟹笼回执与前态，复用原日志reader、CurrentMasterAngler绑定、原比较/编译执行器；缺来源或比较不独立即保留blocker。详见[本项接续合同](master-angler-local-native-join.md)与状态第6节。既有冻结并集`6/19、7/19、6/19、5/11`及formal=false不变，无worker/API，不提交local-data。

- **2026-11-05 最终合入、用户汇报点（本条优先）：** #437最终头`32f43fbb`的CI`41985920364`全部成功，已合并`67f82eb6`；本轮代码分支已核实MERGED/祖先关系并清理。Rusty Key按R2的criterion-local整项已收口，`executable_frontier ∩ Teacher/native三分区`为ready且无本项open work，计数冻结为`6/19、7/19、6/19、5/11`。下条“待CI/提交/合并”已经完成，不得重新排为任务。真实三条join/corpus/原coverage、1370 Core与87阈值保护检查、源档/进程/端口和local-data隔离范围见[局部验收](rusty-key-local-acceptance.md)。正式训练、Teacher全局21分和新档零到60仍未完成，不得据本局部ready放行。当前动作是按用户要求汇报，不在汇报前开新root；后续按R2核对真实局部剩余成本决定下一primary，不恢复旧R1固定全序。

- **2026-11-05 Rusty Key局部收口、本条覆盖下方历史待办：** #436最终头`29cd013d`/CI`41981500964`成功，已合并`3ddbfe17`并核实清理本轮分支。新增两个后台静音原生运行分别在第13/17日独立决策、第14/18日真实获钥匙；各13动作经公开build/Verify同SHA。与旧第6日train合成真实三分区corpus，公开build/Verify同SHA`4ae5a1ba...`。原依赖图同步已批准的逐件rolling递推，不再把“一次性凑齐60条路线”当局部前置；完整阈值证书原规则不改，跨根最优日程/新档真实60件仍归L3/L4。原公开coverage/reconciliation已得Rusty `executable_frontier + coverage_ready=true`、本项open work为空；最终Core1370、阈值/资源87项通过、无跳过，EVD-338不变。本切片仍须最终PR CI后发布。完整来源、边界副本前置和全局未完成事项见[局部验收](rusty-key-local-acceptance.md)。计数按旧冻结集合与本项取并集：`6/19 executable、7/19 split-complete、6/19 CoverageGateReady、5/11 root ready`；本次仅Rusty来源的机器请求局部ready count=1，不是覆盖旧全局记录。formal training=false，无worker/API，local-data不提交。**接续：** 提交/CI/合并和本轮分支收尾后，按用户要求汇报生锈钥匙整项；不自动重开旧门或换另一个root。本条待CI不是用户整项最终汇报。

- **2026-11-05 #435已合并，语料接线验证中（本条优先）：** 最终头`b13b2438`/CI`41978682410`成功，合并`dd87b781`；已核实清理对应临时分支。当前同主线新增`RustyKeyTeacherCorpusBuilder`，逐条调用原独立join重建，按真实决策日归组，拒绝重复run/决策组/nonce/动作；原coverage只合并同分区Teacher和native，不绕过method executable与三分区交集。真实旧13动作已生成**一条train**语料，公开build/独立Verify同SHA`fcecbed9711b9a4f89de04b8de5243ae6da6fc5eb17a438edcd76bbdc48c4c7e`。261项相关回归通过，完整game-free与提交CI待收口；合成三分区只用于负例/交集测试，不计native覆盖。**唯一接续仍Rusty Key：** 收口本语料接线，复核原方法/安全60件阈值供给/时限责任，补真正缺失的validation/test原生决策并接原coverage；不重开旧接受门或加完整Day-0反向前置。冻结计数`5/19、6/19、5/19、4/11`不变，整项完成后汇报；无worker/API/新游戏，local-data不提交。详见[语料合同](rusty-key-teacher-corpus.md)。

- **2026-11-05 #435审计P2已复现并修正，待最终CI（本条优先）：** 原`8b85132f`的CI`41976500737`虽绿，不能据此直接合并。真实发送器把`route_wait_until_time/route_wait_max_slices`映射为RPC无前缀字段，新消费者只按同名DTO属性检查而漏过；4个端到端篡改反例已先红（改/删deadline或slice、重算gzip/哈希后误收）后绿。现由Contracts共享别名声明，发送器/DTO/消费者共用，原字节和执行语义不动。聚焦家族239（41新连接检查）通过，原真实13动作公开Verify仍同SHA`8aea3f8e...`；等待本修正头CI，不将旧绿灯或mock样本作为native覆盖。其余下一步及冻结计数不变：本连接合并收口后补同criterion corpus/真实其他分区/原coverage与方法供给时限责任，不换root、不重开旧门，整项结束后汇报。

- **2026-11-05 连接最终验证完成、待提交CI（本条优先）：** 新连接公开build与最终独立Verify同SHA`8aea3f8e45e12676a663082ba709c24049d424b9c248d5ec902971ec3723d795`，81源文件哈希，原13动作/原生保存/最终key完整绑定，第6日决策、第7日终态、仅train。Core1344、Backend565、方向/博物馆家族235（含37新连接检查）、原/扩展请求日志17全部通过，零跳过、构建零警告错误。源after/实际机械队列路径、RPC缺字段与值、nonce/run/actor/角色/版本/隔离、帧/时间、原生日终和声明篡改均有拒绝反例。完整自读归属保持：原Registry分类和旧预算注记含义保留，单夜immutable回执不升格更广总交接；global-night/full-supply/full-criterion/formal均false。不启动游戏、不改已有native数据、不增worker/API、不提交local-data。**下一步仍同Rusty Key：** 先收口本提交/CI，再复核原方法/安全阈值供给/时限真实责任并补criterion corpus/其他真实native分区/原coverage接线；不能以该单train连接宣布整项完成或换root，也不能把完整Day-0当反向前置。冻结`5/19、6/19、5/19、4/11`不变，整项结束后汇报。

- **2026-11-05 同run类型化Teacher/native连接已实现（本条优先）：** 新`RustyKeyTeacherNativeJoinBuilder`独立重建前态比较和key来源，逐动作绑定原请求gzip/hash/nonce、原机械队列、RPC声明字段、actor/版本/隔离/精确帧、原生捐赠与一项正常保存跨日；只保留原第6日决策分区。原日志校验内核下沉Core，Live原公开reader仅薄封装，writer/格式/字节合同不动，无循环引用或复制校验器。真实输入已构建13动作连接：1041→7053、捐赠289tick、day6→7、仅train；最终新增路径绑定后的独立Verify仍待收口。恢复分类改为复用原Registry（sleep一直是recovery），较新的预算证明注记不当成旧native命令错误；原数据、aggregate语义、可选actual_ticks和更广夜间总门均未改。37项新连接检查、原方向/博物馆家族235通过，较早完整game-free1343/原日志家族17通过；最终测试与提交待收口。**唯一接续：** 完成本连接验证/提交后，按原方法与安全阈值供给/时限的真实责任复核，生成真实决策分组corpus、补缺失的其他native分区并接coverage；不把单train连接升格整项，不改末日凑分区、不重开旧门、不加完整Day-0反向前置。整项/冻结计数/正式训练禁入不变，结束后统一汇报，无worker/API/新游戏，local-data不提交。见[连接合同](rusty-key-teacher-native-join.md)。

- **2026-11-05 #434已合并，唯一接续为同run类型化join（本条优先）：** 最终头`c7ac24fe`/CI`41970315013`完整成功，已合并`49a168e7`；核实MERGED/精确头/祖先关系后删除对应本地和远端分支。当前从main接`feat/rusty-key-causal-native-join`，尚未实现类型化join或晋升整项。原C#请求日志reader只读重验13条动作的gzip/hash/nonce、逐动作queue/item/状态/tick及严格前后连续性：第6日1041捐赠59→60，原生保存4194→4539跨至第7日，6033→7053为既有key终态；共享sleep边界校验通过，未启动新游戏。输入索引`I:\StardewAITraining\rusty-key-terminal-source-20261105\retained-donation-key-chain-index.json` SHA`1a4506e9a6f0bc09605fdcc68c78e830db9e93cea6d0500faee8c768fdbff4e4`，其Teacher/native join与整项标志仍false。提取器初次误用中断动作的父计划路径；现优先原`mechanical_compiled_queue_path`定位实际队列，`effective_compiled_queue_path`保留父计划用途，旧writer/数据不动。**下一实现：** 类型化join独立重建比较和key来源，精确绑定这13个逐动作来源、原生奖励和正常保存跨日，分区始终取第6日决策。必须复用原请求日志读取校验，Bootstrap不可反引Live形成环；若需共享读取层，将原读取内核下沉Core并保持Live公开接口/原writer与字节合同，不复制第二套校验器，先跑原`RuntimeExecutionRequestJournalTests`。其后才补真实其他分区和coverage join，不拿同一观测/末日换标签凑三分区，不重跑旧接受门或附加完整Day-0前置。冻结计数/训练禁入不变，用户要求整项结束后汇报，无worker/API，local-data不提交。

- **2026-11-05 独立捐赠Teacher比较已接入（本条优先）：** #433最终头`89f956ce`/CI`41968263435`成功，已合并`539ddb21`并核实删除本轮临时分支。新比较器只消费原前态和预留账本，复用原候选/空基线timeline/日计划与动作编译，完整保留拒绝行；显式600-tick不捐赠反事实仅供比较，不加入自动候选、不派发、不比较未建模的其他目标。等价捐赠不互造排序，非法方案不作负例。原生前态公开build/verify同SHA`b0b0de31...`：第6日`(O)549`捐赠480估计tick与有界wait均准入，独立pair只属train；第7日key不能改写其分区。实际输入暴露新增消费者需沿用原parent goal以匹配保管授权，原授权规则未改；夹具clock补齐原provenance，不放宽原wait门。30项新检查、原博物馆家族78及完整game-free1307通过，零跳过；提交待CI。**唯一接续：** 沿同run逐动作来源把该独立捐赠安排接到原生阈值/奖励/正常跨日/已类型化key终态，再补真实其他分区及coverage join；不消费control选中项/成功结果造标签、不跨run拼接、不重开旧门。整项未完，冻结计数/训练禁入不变，用户要求整项完成后汇报。详见[比较合同](rusty-key-donation-teacher-comparison.md)。无worker/API/新游戏，local-data不提交。

- **2026-11-05 #433审计修正与Teacher接续依据（本条优先）：** 审计P2已用反例复现：新增消费者反序列化基础回执时丢弃Live原有after-hash/前后tick附加字段，误收同日较晚快照。现读原JSON精确绑定`after_state_hash/before_game_tick/after_game_tick/after_snapshot_fresh`，8反例先红后绿，原Live writer、aggregate语义和可选`actual_ticks`不改。最终55项新检查、原家族120、完整game-free1277通过，旧真实逐动作公开CLI及Verify重放仍同SHA`20bef35d...`；待本修正提交/最终CI，不按旧8737绿灯直接合并。另已核对透明桥完整`commands/remaining_commands`与E实际DLL新反编译：真实event66在命令4自动`RustyKey`，随后7/13的Speak、17的Message才需输入，**不操作仍会先获得key，不能照搬Skull Key“claim/defer”造负例，也不能把自动进度归功给模型。** 因此唯一下一步明确为**此前第60件捐赠安排的独立前态Teacher比较**，复用原合法候选/账本/预算/编译，沿同run已保留的真实捐赠/奖励/跨日接到本钥匙终态；比较日6和终态日7必须按真实决策起点的因果轨迹分组，不能拿终态日冒充决策日或跨run拼成功。原事件推进归机械层；无需新增透明字段/执行器、重跑已接受钥匙门或加完整Day-0前置。详见[决策边界](rusty-key-terminal-evidence.md#correct-teacher-decision-boundary)。整项/冻结计数/训练禁入均未变，完成后汇报，无worker/API。

- **2026-11-05 原生钥匙终态类型化来源已实现：** 新`RustyKeyNativeKeyReceiptBuilder`及build/verify CLI只读消费实际逐动作queue/before/after/回执，复用原候选生成、无Student基线、公开方向绑定和原编译器，不复制Rusty Key归因谓词、不新增执行器/循环依赖。严格校验完整身份/状态哈希/明确tick和Stage-1日期、原事件语义与66终态/key false→true；只忽略原编译随机ID及非事件预算附注，拒绝额外选择/错指纹/坏命令或缺字段。真实输入指出`actual_ticks`本来就是剧情回执未填写的可选项，已修正新增消费者的错误必填假设，原执行器/旧证据不动。47个新检查、原绑定家族112、完整game-free1269通过，Release零警告错误；旧已接受`rk-e8d7a6c3-1104`第7日实际第2动作经两个公开CLI构建/独立重放同SHA`20bef35deabcf916110eeee14bac2553fb527dbbc7ddbd77e46aa03c10a9c456`，没有重开游戏。消费者必须重建原始来源并比对raw JSON，不能信get-only布尔反序列化。详见[终态来源合同](rusty-key-terminal-evidence.md)。代码待提交/CI；**唯一下一步为同criterion的独立前态Teacher比较和三分区corpus/coverage join**，不得从控制选中项/成功结果推偏好，分区必须按原save/player/day计算，不能复用同一观测凑三分区。阈值供给/方法/时限门按R2保留各自责任，不加完整Day-0反向前置。原生来源不等于Teacher/整项完成，冻结计数和训练禁入不变，无worker/API/新游戏，local-data不提交。

- **2026-11-05 合并后唯一接续（本条优先）：** PR #432最终头`32038c65`的CI `41962442740`完整通过，已合并`154cd509`；核实MERGED、精确头和祖先关系后删除本轮本地/远端临时分支。当前`feat/rusty-key-native-terminal-source`从该main接续。先把旧已接受`rk-e8d7a6c3-1104`的原生送钥匙终态接为本criterion类型化来源，再接独立Teacher comparison/native三分区join与原定时限；跨夜保管局部门不再重跑。保留数据只读定位已完成：原C#请求日志reader读取实际第2动作，真实before为前一connector的after，state hash `e04dc35459057a95f0db87839d9e73c700c21735659c8c98a97030e56c8ef127`；aggregate episode起点`565ce8ba...`有意属于整队列，不可拿来验逐动作、也不能修改旧episode迎合新消费者。按实际before重新调用原候选生成/无Student基线排序/`GrandpaDirectionDailyCandidateBinding.Bind`得到ready，原回执在第7日实际key false→true。索引仅本地`I:\StardewAITraining\rusty-key-terminal-source-20261105\retained-key-input-index.json`，SHA`2fb902e05afe8308ed4c9d4ba097b0d0ffbbc2088b6c74952df065ce3fbb0b06`；不是新原生运行或Teacher标签。下一实现必须复用该公开方向绑定/原编译器，不复制原Rusty Key归因谓词或新建执行器；实际queue/item/逐动作before/after/回执、原生event66/非skip、身份/哈希/日期全部核对，错误aggregate起点、外来事件/asset、缺失布尔或日期、旧指纹与不匹配命令拒绝。先做原生来源合同，Teacher比较必须另按独立前态规则生成，禁止从控制选中项或成功结果反推偏好。`LiveTrainingLoop`已依赖Bootstrap，不得为读取日志反向引用Live形成环。**整项仍未完成，冻结计数和训练禁入不变，按用户要求整项完成再汇报；无worker/API，不提交local-data。**

- **2026-11-05 原生跨夜保管已独立复核接受：** clean `3437b6d9`的`cust-night-3437b6d9b-1105`完成32份原生动作回执及6次有界native wait：真实`(O)100`获取/首次古物提示→存箱→第6日19点→原生保存第7日→等待9点铁匠铺开门→取回→09:30捐赠59→60/保管核销。背包`0/1/0/0/1/0`、箱内`0/0/1/1/0/0`，晶球只2→1；账本最终8，原获取SHA保持，3个他目标石头active。原C#请求日志reader核对全部gzip/hash/nonce及逐动作before/after身份/state hash；存/取两份原结算CLI独立重放通过，磁盘Spring8存档仍有产物1和石头3，E基准不变，进程/端口回收。原夜间总门仍`controller_handoff`超时blocked，保留诊断，不抬局部成功为总交接。复核SHA及范围见[接受记录](museum-output-custody.md#accepted-native-overnight-boundary)。该版本完整CI `41959409022`通过；本记录提交CI待收口。**唯一下一步：** 继续同Rusty Key的criterion-local独立Teacher/native终态来源与三分区join、第三年前范围；先消费已接受`rk-e8d7a6c3-1104`钥匙/事件66证据的实际逐动作before和回执，不用aggregate队列起点替代原优先级中断的fresh边界，不重开旧门，不把完整新档60件当局部反向前置。当前只是已接受共享跨夜保管能力，整项未完成，冻结计数/训练禁入不变，`local-data`不提交，无worker/API。

- **2026-11-05 首次古物提示原生反例：** `a3953fbc`的完整CI已通过；新运行已走过控制/提交/派发/绑定并实际开球，但首次`(O)100`古物举物提示令旧晶球教程处理器超时，结果blocked（1000→975、42→43），不得据此结算或声称跨夜成功，原动作未重发。现从E实际DLL重新反编译核对，在原提示处理器按零考古计数、真实接受的Arch产物、完整守恒回执和原生精确本地化文本识别，复用原native-click对话引擎；不增执行器或跳任意对话。Harness构建零警告错误，晶球聚焦11项通过；新固定源码原生验证待完成。源码/失败数据索引见[古物边界](museum-output-custody.md#first-artifact-pickup-boundary)。唯一任务和整项汇报条件不变。

- **2026-11-05 执行绑定范围接续：** `cust-night-bccfaf39-1105`实际原子提交和派发编译已通过，但绑定层还要求未选替代路线的完整比较，获取动作仍为零。现接完这一遗漏：只消费独立来源重建的所选控制准入/精确scope，保留所有所选路线与资源检查；比较不完整时绑定的`selected_from_complete_pareto_frontier`仍为false，落盘绑定再消费必须精确重建，不接受标志绕过。旧泛型/Teacher门不改。保留该次原始提交、队列和14轴输入的完整公开绑定CLI只读重放已ready，无Teacher/训练授权；59项rolling、23项控制（含篡改比较位拒绝）、20项support和30项完整阈值回归通过，Release零警告错误。前两次原生入口失败数据均保留，无获取成功主张；下一步固定新源码后验证真实获取产物跨夜保管，再接原定criterion分区/第三年前终态，不重开旧钥匙/目视门、不抬计数、不提交`local-data`。

- **2026-11-05 跨夜入口暴露获取准入范围错误：** `cust-night-d2d22b3e-1105`在开晶球前零动作停止；实时目标是`(O)100`，已解析晶球路线却被另一条占位`native_object_artifact_spot_chance`的比较不完整挡住。原比较门不删：generic/完整阈值/Teacher仍严格要求全比较；仅原认证控制来源复用共享所选路线安全准入，并显式保留`control_only_incomplete_comparison_scopes`、完整前沿false及Teacher false，未知所选路线/他目标资源仍拒绝。提交重建沿原精确claim验证消费独立重建的控制准入，support控制同用，不增执行器或账本。59项rolling、22项控制链通过；同一失败快照的公开构建/准备只读重放已选合法晶球终端，未改变预测目标或伪造原生成功。真实跨夜仍待新固定源码运行，缺失的artifact-spot日历解析仍是显式缺口。详见[控制范围](control-execution-admission.md#partial-comparison-scope)。

- **2026-11-05 跨夜保管入口已接线：** 原存箱前缀增加`-CustodyOvernight`模式；15点setup仅在测量前，真实获取/存箱后只用原有界wait到19点，再由原rolling回家睡眠/次日取回捐赠。每次等待均保留请求、回执、fresh快照和最新账本；不明回复/时间不前进/身份库存或预留漂移即停，不重试。两处原生睡眠边界共用原校验，不改夜间总门。19项编排、原晚间30/保管20通过，尚未声称原生跨夜保管通过；下一步固定同源码在E隔离单实例后台静音验证。详见[museum-output-custody.md](museum-output-custody.md#native-overnight-continuation)。整项及冻结计数仍不变，不提交`local-data`。

- **2026-11-05 最终合并与唯一接续：** PR #431最终头`31765296`的CI `41951869756`完整通过（Core1222、Backend558、准备61、编排50含可选原生日历夹具、晚间边界30），已合并`717a1845`；核实MERGED及祖先关系后删除本轮修复分支。下方“待CI”均为前态。当前在该main版本上接`test/rusty-key-native-custody-overnight`，尚未新增此切片代码或声称其原生通过：复用原geode获取→原保管存箱前缀，晚时刻setup只能发生在测量前；存箱后通过原有界native wait推进到19点，不用debug改时间/库存，再复用原rolling回家睡眠→次日授权取回→捐赠。检查获取SHA不变、旧位置史不因午夜重铸、被保护的其他目标不丢失，并区分局部保存回执与总交接诊断。之后才接本criterion独立Teacher/native分区与第三年前终态；不能把边界夹具当完整新档60件轨迹。用户要求生锈钥匙整项收口后统一汇报，局部通过不当作整项完成；WIP和冻结计数不变。

- **2026-11-05 本轮收口结果（本条优先）：** 修正已提交`9070f94f`并推送PR #431。本地准备61、rolling supply56、运行编排49、晚间边界30、共享捐赠/睡眠回归86通过；初次CI中旧预算夹具仍期待19点后赴馆，被原恢复优先规则正确拒绝，已修正该测试断言，18:53超预算检查仍保留。真实`evening-9070f94f-1105`完成13份applied/verified回执：第6日19点→原生保存第7日→赴馆/有界等待→08:40捐赠59→60，物品2→2→1；请求gzip哈希/nonce/队列身份全部重验，磁盘新日存档仍有2件，E基准SHA不变。原wrapper最后因夜间总事务的controller_handoff超时退出1；该总门有意不接受`executor.*`为规划工作，未改旧规则。局部校验改为绑定不可变sleep-ready回执及同事务原生保存事实，重放现有成功动作，明确总交接未验证并保留blocked诊断，未盲重跑。游戏/服务/端口已收回，原生证据仅为夹具已有物跨夜及次日捐赠，不是自主获取/产物保管跨夜或Teacher。后续仍为同Rusty Key的真实获取产物跨日保管、criterion独立分区及第三年前终态；不重开旧钥匙/目视门，不晋升冻结计数，不提交`local-data`，无worker/API/训练。详见[本轮原生结果](rusty-key-time-budget.md#native-evening-boundary-result)。最终修正提交及CI尚待收口。

- **2026-11-05 原生反例后的日终恢复修正（当前）：** #430已合并`a911cb53`，对应分支已安全收口。真实隔离运行`evening-46a03dc6-1105`在测量动作开始前停止：新增控制器错误地将正常晚间恢复依赖于完整赴馆路线的纯时间失败。旧`FailureClass`去掉细节后缀是稳定分类设计，不是解析缺陷。现删除新增时间白名单，单一`day_recovery`角色在原19点/精确Sleep提示或原生控制恢复条件下先复用旧候选和编译器；不要求库存覆盖整个差额，也不要求博物馆路线可达，实际捐赠前仍严格验证路线。身份/账本/库存不可读和原恢复不可派发仍阻塞，不新增执行器、不放宽原路线，不把跨夜当未来路线证明。下方两条早期“足够库存+纯时间失败”规则已被本条撤销，仅作历史。当前修正待针对性验证、真实快照只读回放和全新后台隔离运行；未新增原生成功、Teacher或训练证据。WIP与冻结计数不变，不提交`local-data`，不调用worker/API。

- **2026-11-05 跨日保管原链复核与晚间入口准备：** #429最终CI通过并合并`4ba99e18`（Core1222、Backend558、准备54、编排46；本地无可选native-calendar夹具为45），本轮分支已删。原`FailureClass`和解析器未改；先前错误格式误判是新消费端未对齐旧分类，不是旧解析器缺陷。原保管位置史已正确支持次日：4个公开构建/准备样例验证同一来源/位置SHA保持、fresh次日日期路线接续、旧日期不升格、缺物/坏位置史拒绝；无须改业务代码，准备共58项通过。原threshold smoke增加`-OwnedSupplyEvening -SupplyMode held_item`，全部setup在测量前，以原透明home入口/19:00/既有59件边界准备后，只调用原rolling普通恢复→次日捐赠，到typed threshold停止，不重测旧钥匙门；21项无游戏边界检查通过，原晶球33/保管20/钥匙证据合同41项兼容保持。夜间事务只消费同run实际save commit/次日/安全恢复事实，不反向要求旧Farm/event66门。入口尚未原生运行，明确不证明真实获取、产物保管跨夜、criterion或Teacher总验收；代码待CI收口。夜间不启游戏/worker/训练；下一步在允许运行时用同一隔离进程执行晚间边界并独立复核，继续按原合同关闭合法日期/保管及本criterion分区与第三年前终态，不新增全局反向前置，不提交`local-data`。

- **2026-11-05 晚间待捐续接：** #428最终CI通过并合并`e402af4a`，对应开发分支已删。本轮仍只做`obtain_rusty_key`：足够不同已拥有待捐物、原编译/完整日期路线明确时间排除后，复用原回家/清障/睡眠、精确Sleep提示和30tick原生控制恢复；未增执行器，不强制白天睡觉，不从缺字段/校准或授权失败兜底。原准备完整重建，原适配器每步保留最新账本后fresh-replan；D日睡眠须给D+1捐赠、D+2实际奖励留第三年前余量，未来路线仍未验证。最终本地准备54、运行编排45、共享捐赠/恢复聚焦137项通过，显式E构建零警告错误、检查点EVD-338不变；全是无游戏合成/实际CLI证据。正常白天held路径保留按需库存读取，不增逐帧扫描。代码待CI收口；下一步是合法后续日期/跨日保管的真实边界、criterion独立分区及第三年前终态。不重开旧已接受门，不晋升冻结计数，不提交`local-data`，无worker/API/游戏/训练；合同见[晚间续接](rusty-key-time-budget.md#owned-supply-evening-continuation)。

- **2026-11-05 同切片实际接线：** 原运行脚本第一次轻量控制未转发已有`RouteTimingPath`，已用实际远端held反例复现停滞；现原common参数同时带入构建与准备。四条实际CLI轻量路径验证校准不遗漏、不重建无关获取轴；最终准备34、运行编排39项通过，无游戏/HTTP/训练。下方33项是追加夹具前的历史状态。仍在同一个PR内等待最终头提交CI；不重开已接受共享原生/钥匙/目视门，不晋升计数或训练。

- **2026-11-04 当前完整取回续程：** #427完整CI通过并合并`d3e5b75d`、分支已删。当前仍只做`obtain_rusty_key`：原选定保管物绑定reservation、位置史和目的节点，复用现有日期路线/校准，完整到箱后加原交互预算再赴馆；本地遵守原编译站位，远端严格绑定实际首段，不把首段耗时当完整取回，也不重复计候选移动。原即刻转移/单段导航/有界等待、fresh授权/容量和原生结算不变；清障后未观察几何等未知明确保留，不重获库存。聚焦63、准备33项通过；保留真实取回前快照/修订5账本的公开构建和准备CLI只读通过，09:00→09:08→09:42柜台→09:48捐赠上界。最终CI待收口。下一步接合法后续日期与跨日保管、真实次晨钥匙及criterion独立分区，不重开旧已接受门、不抬计数，不提交`local-data`，无游戏/worker/训练。见[时间合同](rusty-key-time-budget.md)。

- **2026-11-04 当前取回时序前置修正：** #426完整CI通过并合并`68020b69`、对应开发分支已删。准备完整到箱→取出→赴馆日程时，实际运行器180/120tick等待与旧候选120tick、primitive`120+数量*2`及日计划1分钟终端预算不一致。现由Contracts共享原成功上界`306+数量`tick（1件保守8分钟），移动/交互各计一次，非法投影不产生短耗时，溢出饱和。Harness依赖边界不变，显式E构建零警告错误；修前8反例失败，最终聚焦109项通过，CI待收口。不改原生行为/等待上限、26:00机械边界，不新增执行器。下一步复用原授权取物/日期路线，补完整到箱路线与取出再赴馆，之后合法后续日期保管/真实次晨交接及criterion独立分区；不重跑旧已接受门，不抬计数，不提交`local-data`，无游戏/worker/训练。

- **2026-11-04 当前所选获取续程：** #425完整CI通过并合并`929686f3`，本轮开发分支已删。当前只对原控制器选中的获取方案，绑定原daily预算哈希、保守完成时刻和最终站位（多步必须与末步一致），复用同一个赴馆排程器；不同出发时刻/站位不可串用缓存，不修改快照或捏造held库存，原排序/准入/执行器不变。当天赴馆未证明保留为显式缺口，不冒充获取本身非法或否定次日保管方案。聚焦55、rolling56、准备27通过，公开原生获取前输入只读重建给出09:10铁匠铺→09:45柜台→09:51完成预测；最终CI待收口，无新游戏/worker/训练。下一步完成授权取回时间、合法后续日期与保管、第三年前真实次晨交接及criterion独立分区；不重跑旧钥匙/目视门，不抬计数，不提交`local-data`。边界与证据见[时间合同](rusty-key-time-budget.md)。

- **2026-11-04 完整当日赴馆路线：** #424已由完整CI接受并合并`f8172eec`、对应开发分支已删；下方待CI记载是历史状态。当前仍只做`obtain_rusty_key`：held分支复用原日期路线/开放窗/校准，绑定实际首段并证明后续全部门、柜台到达及原240ticks捐赠预算；按完整完成时刻选合法路线，保留替代首段，进门关闭时间不约束已进馆的室内捐赠。等待开门候选复用原`ScheduledRouteWaitPolicy`，修正held分支提前丢弃这一接线遗漏，原编译/JIT仍只发一个连接器或一次有界等待后fresh-replan。rolling49、准备25、Backend558/558通过；首个PR提交已通过含Core1193的完整CI，自查另补必须经过首段时的同地图零段绕过与真实往返，聚焦73项通过，最终CI待收口。保留`custody-b3512d5f-1104`取回后真实快照/账本修订7的公开构建和准备CLI只读重放通过，09:34柜台/09:40捐赠完成预测，不是新原生执行。证据与最终SHA见[时间合同](rusty-key-time-budget.md)。下一步仅补所选获取真实终点/完成时间、授权取回与完整赴馆路线的顺序组合，再接合法后续日期、第三年前真实次晨交接及criterion独立分区；不把本段当完整联合证书，不重开旧原生/owner目视门，不抬计数，不提交`local-data`。本轮无游戏、worker或训练。

- **2026-11-04 时间门修正：** #423已由完整CI接受并合并`e639b24d`、开发分支已删。当前联合时间窗/预算切片先修原held捐赠分支缺预算及通用时间窗tick换算不一致：原日计划在18:52允许夹具8分钟动作、18:53上游排除；缺失/不可读/格式错/HHMM错/双字段冲突均无队列，保留pending库存且不重复获取。原保管严格时钟读取提取复用，不改其26:00机械边界；调度器只统一动作耗时，不重编码WaitCost或改变有界等待。修前反例已复现，24聚焦/44rolling、Core1180/1180、Backend558/558、准备25项通过，最终CI待收口。无游戏/worker/训练，计数冻结。后续顺序组合日期绑定的获取完成、存取、完整赴馆门与柜台、次晨交接，不能把本地时间门当完整联合证书，见[合同及退出边界](rusty-key-time-budget.md)。

- **2026-11-04 真实保管闭环已接受：** `custody-b3512d5f-1104`同一隐藏静音隔离游戏中完成6队列/7条fresh、applied、verified原生动作：获取`(O)549`、原控制存箱、rolling自动取回、两次连接器、原捐赠/核销；无重复获取或自由副本替代。背包`0→1→0→1→0`、箱内`0→0→1→0→0`，晶球`2→1`、捐赠`59→60`，账本修订`1/3/5/7/8`；他目标3石头active且槽9→1。局部退出`native_donation_threshold_observed`，不再执行夜间/钥匙。独立调用原转移结算CLI两次、原捐赠构建器、原只读文件回执查询及原服务精确重放均通过；全部阶段状态哈希、runtime源码等价/二进制/Mods哈希、源档哈希与进程/端口释放通过。原始数据在`I:\StardewAITraining\custody-round-trip-20261104\native\custody-b3512d5f-1104`，复核`independent-review-03/review.json` SHA=`681f33b01cd724f8f164a62e8920c6a98a4e860c8190b75e8a35564a3e6d238a`。下一步联合时间窗/预算，再按既定criterion分区与第三年前时限推进，不重开本次或旧钥匙/目视门，不抬覆盖/Teacher/训练计数。代码收尾仍需最终CI合并；不提交原始本地数据。

- **原生前缀已真实获取，修正脚本边界而非引擎：** `b84ced79`已实际开晶球并经原结算保管一件`(O)549`，但脚本把高层`museum.donate_items`当作底层消费者，存箱前错误拒绝。现直接消费原terminal结算返回的保管ID，消费者/目标/位置授权仍交原C#构建器；同一保留原生数据的真实只读准备CLI已返回ready，无新动作/提交。另修正本轮退出范围：显式`StopAfterDonationThreshold`只在原C#重建的typed threshold control成立时停止，不再把已接受跨日钥匙作为存取捐赠局部门；默认钥匙运行不变。下一步新run继续真实存→取→捐赠，不重放旧动作，计数冻结。

- **原生准备参数修正（仍无获取/搬运）：** `6d055096`已真实准备Blacksmith稀疏箱/他目标预留和晶球边界，随后因PowerShell数组表达式将参数名与路径拼接而在子脚本参数绑定处停止。现按原仓库`List<string>.Add`逐项构造；新增带空格输入路径的真实pwsh子进程绑定检查，必须到达缺失隔离目录的预检、不得HTTP/原生派发，边界检查共20项。进程退出，旧run保留，不回放。

- **原生边界首试定位（尚无获取/搬运）：** clean`f2b0c4ef`在setup阶段以`material_transfer_fixture_target_unavailable`停止，进程/端口已退出。核对已接受原生Blacksmith快照为15×20，旧农场夹具搜索`width-16`导致零候选，并非原生禁放。现只改通用两格边界，保留walkability/canBePlacedHere/相邻站位检查；边界检查补至19项。原失败证据保留，接续必须新run身份，不重发旧测量动作。

- **#422已收口并进入真实边界准备：** 完整CI通过，合并`923c8f2e`并删除该轮本地/远端分支。原threshold smoke可选`-CustodyRoundTrip`仅用于current_geode：沿原夹具的显式地点准备Blacksmith稀疏箱，经原账本保护他目标石头；测量开始后同一游戏run真实获取、原控制存箱，再交给原rolling自动取回/捐赠。一个动作预算的获取前缀仍exit2、不冒充成功；拒绝玩家自由副本，最终要求获取回执来源不变、保管核销、他目标预留及压紧槽位正确，无中途fixture/物品注入。新边界18项、原晶球边界33项通过；Bridge/Harness/Backend Release构建零警告错误。下一步clean source构建后隐藏静音运行并独立复核真实轨迹；当前尚无新原生通过或计数晋升。

- **2026-11-04 rolling 仓储取回接线：** #421已合并`decc7ce8`、最终CI成功并删除对应开发分支；下方该轮待CI记载已过期。当前rolling在新获取前选择有完整位置史、归属当前目标/消费者的箱内保管；显式区分`custody_navigation`（原连接器/清障/有界时间窗等待，无提交）与`custody_transfer`（原搬运提交请求，实际提交后重建原编译/JIT），保留95项分母、待捐保护门与原捐赠核销。运行客户端读取Live实际派发命令及逐动作before/after原路径，经原journal/只读恢复调用原结算并独立重放，缺失/篡改命令或重复转移步不能结算、不重发动作。延迟通行判定与既有容量恢复统一复用；锁门/普通连接器合法等待通过，关闭/未知路线仍拒绝。Backend558/558、完整Core1156/1156、准备25项、真实CLI客户端6项通过，无新游戏/Teacher/训练证据。下一步是同源隔离run真实获取→原控制存箱→rolling自动取回→原捐赠核销，核对全部槽位和他目标保护；setup只在测量前，不重复已接受钥匙/目视门，不抬计数。详见[运行合同](rusty-key-runtime-rolling.md)。

- **2026-11-04 保管搬运控制/编译/JIT接线：** #420已合并`35af02ce`并删除该轮分支。新保管搬运请求复用原候选、日内时间预算、提交、日计划/动作编译与原子结算；提交和结算结果均由原服务独立精确重放。专用控制绑定保管ID/行修订/位置SHA、准备/全局修订/目标，不带`acquisition_*`，不能把输出搬运冒充机器获取；旧普通搬运仅可被动压紧有完整历史的保管，主动选择仍需专用授权。原Live/Backend HTTP已验证移动后仅重绑state hash、两侧完整before/after库存核验及原结算，移走原before缓存仍可结算。三个真实只读CLI与原请求/队列/结算一致，错误目标exit2且无请求产物；紧凑本地夹具为`I:\StardewAITraining\custody-control-20261104\public-cli-01`，不提交。意图编码与队列ID稳定器复用原代码，不增快照缓存/逐帧扫描/账本/执行器。当前仍是合成/API/CLI证据；下一步把仓储待捐选择接入原rolling准备与运行客户端，再取得真实获取的存→取→捐赠证明，不重复已接受晶球/钥匙或目视门。见[控制合同](museum-output-custody.md#original-control-and-compiler-binding)。
- 本轮验证：Backend543/543、完整Core1156/1156、控制/拒绝42项加原被动保管派发1项、原canonical机器转移终端链及rolling34通过；三条真实CLI及错误目标/篡改结算结果两项exit2拒绝通过，坏结果不暴露可重发请求。Release构建零警告错误，检查点仍EVD-338；最终CI待完成。补齐旧夹具缺少的当前地图/时钟，不默认生产未知值；初次缺少receipt命名空间已修正。coverage/Teacher/正式训练计数不变。

- **#420审查修正：** P2已用两个原入口反例复现：无预留副本的真实捐赠不应核销箱内仍未动用的保管。原请求构建器/账本核销现共用玩家持有消费授权，最终Backend501/501，原存→取→捐赠保持通过；合法箱内位置史与待捐库存识别不变。候选/派发原已有背包限制，未发现同类遗漏。本项仍不开放JIT迁移门或提升coverage/Teacher/训练，接续顺序仍是下条原控制/编译/派发和真实存取捐赠。

- **2026-11-04 保管逐次位置证明已接原服务：** #419已合并为`32c78ab5`并删除该轮开发分支。原准备/结算服务现仅对有初始锚点和完整位置史的保管开放原子搬运；每次在原relocation/rebind历史附紧凑证明，绑定前后行修订/位置、前一证明、同一准备和fresh回执，获取来源/消费者/归属/数量不变。他目标搬运导致的被动压紧也保留保管自己的来源。原API存→取→原捐赠核销、文件重启/只读丢回复查询及篡改/失败回滚26项通过，Backend499/499、完整Core1155/1155通过；旧signed-ID测试只改ID未改player节点，已修正夹具全链身份，不放宽生产校验。只存紧凑历史，不增全量快照或另建账本；legacy不补猜来源且无迁移权，无准备的通用relocate/rebind仍拒绝。这是合成服务合同证据，无新原生/Teacher/训练晋升。下一步接原控制候选/编译/JIT/回执及真实获取的存→取→捐赠，原JIT保管迁移门仍关闭；不伪装为机器输入获取，不重开已接受晶球→钥匙或目视门，见[位置史](museum-output-custody.md#linked-position-history)。

- **2026-11-04 保管初始位置绑定：** #418最新提交完整CI通过，主线合并`abb6c45a`，本轮旧开发分支已删除。原获取结算在同一修订创建可选初始位置记录，并在原创建历史锚定SHA；新记录校验不可变获取元数据与初始/当前node、slot、state、row revision，普通全局修订/时钟刷新不改原始事实。账本复制深拷贝并保留历史锚点，真实文件仓储重启不变；旧无该字段记录维持原消费者行为/序列化，不回填或新增迁移权。Backend473（新增23）、Core聚焦80、原Rusty Key rolling34通过；全套Backend改用白天4逻辑核/BelowNormal后14秒通过，2核旧恢复超时保留为负载观察，未修改超时/断言。无游戏/训练和计数晋升，主动/被动保管迁移门仍关闭。下一步是原历史中的逐次位置证明：绑定前后行修订/位置、同一准备和fresh回执，原子更新完整map并保留来源，再接原控制候选/编译/JIT/核销，最后验证真实获取的存→取→捐赠；不能把初始绑定当成迁移闭环，见[合同](museum-output-custody.md#initial-position-binding)。

- **2026-11-04 原canonical准备绑定接线：** #417已合并并删除本轮开发分支。本轮沿原生产者/commit回执/编译/JIT/Live派发/settlement回执接通准备SHA；共享准备校验也用于原服务，不再私有复制。绑定缺失不能将有预留的搬运降级为无保护旧命令；正常移动只重绑派发状态，不重写准备来源或目标。原结算回执保留完整map/SHA，独立精确重放包含他目标压紧行及各自来源。#418审查P2已核实修复：派发前校验实际执行JSON，与Live同用Web解析，意图须匹配标量/准备，物理投影须匹配原投影器重算结果；实际Live构造器证明坏JSON会被消费而新门先拒绝。Backend最终450（新增36），本轮Core1155及原有界canonical链通过，含他目标石头槽9→0、核销/终端续接及错误绑定/map反例；检查点仍为EVD-338。初跑CLI失败源于11月2日旧产物，重建同配置原PolicyModel/PolicyDataset后通过；旧museum恢复用例另有一次15秒外层超时，未修改的14项隔离组和最终450全套均通过，未放宽阈值。无游戏/训练，覆盖冻结、保管迁移保护门未打开。下一步沿原合同补不可变获取溯源/授权可变位置及真实存→取→捐赠；[保管细化](museum-output-custody.md#remaining-location-boundary)说明当前创建历史不是完整位置史，缺原始事实不可猜，不新增事务/账本/执行器。

- **2026-11-04 普通转移的准备/原子结算切片：** 原portfolio提交/结算API现以同一账本的一条有界准备记录保留两侧库存、意图、完整map、全部物资预留元数据哈希和来源历史，不依赖已被2快照缓存淘汰的before。原事务一次更新受压紧影响的全部预留，保留其他目标/数量/归属；成功移除准备，任何核验失败仍是原账本，不能覆盖准备或换portfolio省略绑定。正常预算预留继续沿用原规则。Backend414（新增30）/Core1155通过，含真实HTTP/文件重启/快照淘汰/丢回复恢复及主动、被动保管拒绝；保留原生快照的独立离线文件重放验证每方向15条合成预留、最终修订6，零新原生动作。#416已合并并删除本轮旧分支。下一步是原canonical生产者在commit前生成准备请求、commit回执重建并绑定准备SHA、编译/JIT核验同一准备，settlement及独立精确重放消费同一map/来源历史；然后再接真实获取的存→取→捐赠链及保管授权。不要再实现第二套事务/账本、扩大全量快照缓存或提前删保管保护门，所有覆盖/训练计数冻结，见[合同](material-transfer-slot-lineage.md#prepared-atomic-settlement)。

- **2026-11-04 稀疏箱原生回归已独立复核通过：** clean `0cdc3d8d` 脚本与同源四组件构建，在独立存档/账本/精确Mods/隐藏静音的 `material-native-open-1104` 完成取2还2；两次原生菜单/解锁、四次单位点击，木头槽5→0、石头槽9→1、最终11木头/3石头，错误投影零点击且两侧槽位完全不变。独立调用原C#投影/完整槽位验证器，重核二进制哈希、账本起始0、源档哈希及进程/端口退出，均通过。旧X开箱/释放状态已删除，复用现有 scoped 右键边沿调用原生checkForAction，不直接开菜单/取锁或改库存。前置无游戏20/Backend384/Core24，本轮聚焦7项、四组件零警告/错误构建通过。下一步不重复该回归：在原账本/准备结算链实现完整原子保管迁移/rebind，保留获取溯源，并按完整lineage同时迁移受压紧影响的其他目标预留；不新增执行器/账本、不直接删保护门。coverage/Teacher/正式训练仍冻结，详见[接受边界](material-transfer-slot-lineage.md#accepted-native-boundary)。

- **2026-11-04 09:36 原生当前晶球闭环已独立复核通过：** `e8d7a6c3` 脚本、逐文件核对无C#差异的clean `8fe21913`构建，在新隔离副本中完成15次队列、16条verified原生动作：真实晶球→一件保管→第60件捐赠/核销→回家→正常保存跨日→Farm事件66实际取钥匙。产物0→1→0、晶球2→1、账本0→2创建保管→3核销；原C#核销请求独立重建哈希一致，夜事务complete且下一日安全动作为exploration，源档哈希未变，进程/端口已回收。精确e8 CI编排34、Core1123、Backend381、边界41全部通过。原始数据仅本地；此为59→60共享能力，不是从零60件、criterion/Teacher或正式训练晋升。下一步不重复这条已验证链：先将旧物资转移smoke接共享独立存档/账本/隐藏进程与skip-training合同，补稀疏箱原生lineage回归，再做完整原子保管迁移/rebind；保护门不提前打开。详见[已接受边界](geode-dialogue-lifecycle.md#accepted-native-boundary)。

- **2026-11-04 已验证正常保存跨日，清晨等待顺序修正：** `40dad9f6` 连续完成真实获取/保管/捐赠、四次地图后恢复等待、返家及原生睡觉保存到第8日。夜事务已观察save commit/expected day/safe ready，但仍待下一日安全动作，不能写成complete或拿到钥匙。600时上层先请求出门，已有安全层实际先等待，因此严格包装器拒绝；现清晨先查询同一恢复生产者，有精确等待就先编译它，没有等待才出门，绝不选择该查询返回的再次睡觉候选。35项本地检查通过（默认34，另1项真实清晨回执/报告adapter复核），所有mock参数还经过实际CLI Parse，零目标确实被拒绝；测试替身非零退出码已限定在测试内回收，修复“30项通过但CI外壳红”的问题。待最终同版本连续钥匙链；覆盖与训练锁不变。

- **2026-11-04 控制等待不冒充工作动作：** clean `8fe21913` 再次真实获取/保管/捐赠，并验证30 tick等待恢复 CanMove；原CLI正确不把等待计入训练工作。`18b59881` 的零工作目标方案被回执模式启动校验拒绝，未执行等待，这是编排修正失误。最终保留1个工作动作目标，仅在精确applied/verified/completed等待、fresh快照及同run/queue/before hash报告证明一次尝试、零工作动作、唯一目标未达原因时识别exit 2为观察完成；其他失败不放行。30项无游戏回归加旧真实等待回执/报告的adapter复核，共31项通过；不是新的原生动作，仍须复跑整条钥匙链。仅PowerShell编排改变；复用前一clean构建须核对C#源码树无差异、哈希/版本一致并分别记录脚本/构建提交。

- **2026-11-04 晶球/捐赠原生链通过，跨日续接修正中：** clean `45645d96` 已真实确认首次晶球提示并恢复原生控制，获得 `(O)549`、canonical 输入核销/一件保管，再进入博物馆完成第60件捐赠与保管核销。返家第一条 connector 后只提供已有控制恢复等待，脚本却只选睡觉候选而停止；新旧 Rusty Key 入口现共用精确等待/正常跨日选择，仍走原日计划、有限步数及 fresh replan，不清 native 标志或重放失败动作。最终同版本闭环待复跑，未取得钥匙/Teacher/criterion 验收，见[原生记录](geode-dialogue-lifecycle.md#latest-native-boundary)。

- **2026-11-04 首次晶球提示续接：** `ff77caad` 的 UI 修正已通过 CI，但更严格原生复跑在真实首次拾取晶球提示处安全停止，未结算获取。已直接核对 Farmer/Item/DialogueBox 原生规则，补 geodeFound 观察、候选/编译/请求/JIT 绑定及预计邮件副作用；缺字段不默认 false。仅在库存回执、原生标志、精确原生文本及无问答/剧情条件都匹配时，原晶球动作复用现有普通对话执行器确认提示，最终仍要求完整邮件守恒和恢复控制；不另写对话引擎或屏蔽提示。11项聚焦通过，最终 clean source 原生闭环仍待复跑，详见[生命周期合同](geode-dialogue-lifecycle.md#first-pickup-continuation)。

- **2026-11-04 已取得原生获取/保管，续接开晶球对话生命周期：** #412 已合并，精确 `8226ae8c` CI Core `1112/1112`、Backend `381/381` 通过；独立账本从0起，真实开晶球获得 `(O)549` 并 canonical 核销输入/保管一件产物，随后两条 connector 到博物馆，但尚未捐赠。三份 after 均显示开晶球结束起就残留 dialogue_up=true、无菜单/剧情且 CanMove=false，保护门正确阻止重复获取/释放保管；源档哈希未变、进程已回收。反编译确认直接 answerDialogue 跳过 DialogueBox 原生收尾，现复用原对话选项点击为共享 helper，晶球完成还需原生控制恢复，不手工清标志。聚焦37项通过，下一步是 clean source 同入口原生获取→保管→捐赠→核销/取钥匙，不重开目视验收、不提升计数，见[合同](geode-dialogue-lifecycle.md)。

- **2026-11-04 原生尝试定位空闲暂停接线：** clean `308d701f` 的第五年基线先被原第三年前时限门正确拒绝；改用第一年春7日 ProofFarm 隔离副本后，真实源链暴露 harness 自持空闲暂停未透明给时间校准，且 location 丢失具体失败原因。两次均无获取/捐赠派发，不能记作闭环。现复用原暂停分支补只读 ownership/epoch API，桥保持真实暂停/未 ready，校准只接受精确自持、可派发前释放且其他原生门全开的暂停；普通暂停及不兼容仍拒绝。补 XML 日期启动前检查，避免再开明显过期的基线。聚焦校准39、边界32和 E 路径构建通过；同版本原生闭环仍待验证，不放松严格 frontier/未知路线或提升计数。详见[合同](executor-owned-idle-pause.md)。

- **2026-11-04 保管迁移前置：原生槽位 lineage 修正：** 直接核对锁定反编译确认箱子存取会压紧空槽、玩家移除只留空位；现有取物执行器固定旧槽索引会在稀疏箱第一下后失效，独立回执也误用压紧后的索引读取 before。现复用原转移投影导出两侧完整槽位 lineage，原生点击跟踪原物品引用，原回执独立验证包含其他物品的槽位/质量/runtime/数量，结果和 episode 记录实际剩余源槽；不另建执行器或每 tick 快照。局部投影/回执、完整 Backend `381/381`、原控制 support `20` 与原 canonical 机器转移→核销→终端链通过，均为无游戏证据。保管 relocation/rebind 保护门仍关闭，未提升覆盖或训练。静默解除后仍先同版本 current-geode 原生轨迹，再补稀疏箱原生回归及完整保管迁移，见 [合同](material-transfer-slot-lineage.md)。

- **2026-11-04 live 核销调用点补齐：** 排查位置迁移时发现，前一轮恢复只覆盖 rolling 脚本，Museum 与 Rain Totem 的 C# live 核销仍直接 POST。两个现有钩子现共用同一发前留档/单次只读查询 helper，复用原 Backend 日志与查询，不新增执行器或账本；明确 4xx 不查询，未知/错绑定仍保留原失败。恢复结果还需精确绑定 portfolio/source/receipt/revision/state，并继续原 completed-ID/失败关闭路径。Backend `381/381` 通过，含真实仓储/HTTP、实际 live 方法、坏拒绝正文/有界正文读取及无窗口 PowerShell 读取 C# 日志的跨进程验证。未启动游戏、未训练或提升覆盖。原生同版本轨迹仍待静默解除，随后接位置迁移等既定缺口，见 [恢复合同](portfolio-mutation-reply-recovery.md#live-material-settlement-hooks)。

- **2026-11-04 Backend 变更丢回复恢复：** 原文件账本在同一次原子发布中保留最后一次成功 portfolio commit/终端结算/准备结算的精简实际回执，不复制完整账本或快照。独立只读查询不调用会自动核销的 `Get`，不改缓存/账本/审计；精确请求、身份、修订及哈希匹配才还原回复。原 rolling 客户端发前留原始请求，失败最多查一次，仍交原 C# canonical receipt/verifier，未知或被后续版本取代即停，绝不重发变更/原生动作。本项不替代同版本原生获取→保管→捐赠→核销轨迹，primary/覆盖/正式训练锁不变；原生下一步仍待静默解除。验证与边界见 [合同](portfolio-mutation-reply-recovery.md)。

- **2026-11-04 Product 丢回复恢复：** 原 live 发送点现先持久化完整请求 gzip、nonce 和原始/Product 两种哈希，再发 RPC；不新增全量快照。Product 复用原 pending/final 日志提供独立只读查询，且已授权派发在 durable intent 后由服务端按原生截止时间负责收尾落盘，不再被断开的客户端取消。传输失败只查一次，只有精确绑定同请求的 final 回执交回原 fresh/结果验证；pending、错身份或查询不可用仍停，绝不重发原生动作。独立 CLI 同样只导出观察、不结算或训练。已有 HTTP/实际 CLI、丢回复/取消/截止时间/保留 pending/防篡改反例通过；本轮未开游戏、计数不变。原生下一步仍是静默解除后同版本 current-geode 获取→保管→捐赠→核销；Backend 变更丢回复及迁移等缺口尚未闭合，见 [恢复合同](product-receipt-recovery.md)。
- **2026-11-04 原生获取边界准备：** 现有 threshold smoke 新增可选 `current_geode` 模式，复用原启动隔离和 rolling 入口；原固定物品模式不变。准备阶段从实时原生预测选择可捐目标，博物馆夹具按原生注册表/适捐规则排除该目标，再由原开晶球夹具清空目标库存并恢复相同输入。验证目标未捐、背包为零及身份/预测不漂移后才结束 setup；所有测量仍走原执行/保管/捐赠链。24 项准备/启动前拒绝检查和 Harness 构建通过，尚未开游戏；不晋升任何覆盖。夜间静默解除后的唯一原生下一步是 clean commit 上运行此模式并独立复核完整获取→保管→捐赠→核销轨迹，随后继续失败回执恢复，见 [运行合同](rusty-key-runtime-rolling.md#native-acquisition-boundary-preparation)。
- **2026-11-04 当前原生源日历纠正：** 复现静态日历会丢失当前开晶球等 live-only 来源，故受控 Rusty Key 入口改用共享编译器的 `current_sources` 模式，保留当前快照和完整物品/路线分母；只将未请求当前分母的 CC 路线明确阻塞，不弱化全局 current-CC 校验。日历与目标日历绑定快照 SHA/state/save/player/version，下游独立重建；14 项日历及 13 项真实源流水线/编排检查通过，含开晶球终端与篡改反例。此条取代上一实现的静态模式建议，仍无游戏/新原生证据、无覆盖晋升。当前唯一下一步仍为同版本原生获取→保管→捐赠→核销边界轨迹及失败回执恢复，见 [运行合同](rusty-key-runtime-rolling.md)。
- **2026-11-04 受控运行编排入口：** `Invoke-RuntimeRustyKeyRollingSupply.ps1` 已串接原 14 轴流水线、C# 控制来源/当前源绑定排名、提交前判定、HTTP 原子预留、原 live 队列、canonical 获取/准备结算及 fresh replan；捐赠后的账本也单独留存。运行步数/磁盘底线有界，旧 run 拒绝重放，网络不确定时保留现场且不重试原生动作。普通提交回执 CLI 支持独立验证控制 support lineage。日历现按上条修正为 `current_sources`，其他调用仍默认严格 current 日历。共享日计划适配器保留原快照清单位置并显式传递 product/harness 选择。实际 CLI 源链与编排双重测试通过；这是代码/合成证据，尚未启动游戏或取得本入口的原生获取→保管→捐赠→核销轨迹，计数不变。下一步优先原生边界轨迹及失败回执恢复，见 [运行合同](rusty-key-runtime-rolling.md)。
- **2026-11-04 提交前执行准备与待捐库存：** 新 `prepare-rusty-key-rolling-step` 独立重建控制来源后，在真实提交前区分已有库存捐赠、准备动作、终端获取和钥匙原生交接；终端预编译复用原派发编译方法，准备动作复用原 request builder，不新增执行器或账本。回归复现并修复“物品已持有但暂不能捐时重复获取”，共享物资投影扣除他目标预留，仅回加有创建历史的本目标保管；仓储待转运不冒充已捐。已有捐赠计划/队列复用共享确定性 ID 稳定器，保证同源重建。新增编排 18、rolling 34 项无游戏检查通过，原控制 20、support 18、阈值及编译/监督回归通过；未启动游戏、未提升覆盖。下一步仍须把这些真实入口接成运行编排及同一原生获取→保管→捐赠→核销轨迹，不能把本准备报告当成自主运行完成。见 [提交前准备合同](rusty-key-step-preparation.md)。
- **2026-11-04 控制来源的准备动作重绑：** 原 supporting-transition 结算与 fresh replan 现在可供 rolling 控制选择独立消费，再接原 support-aware admission/commit receipt 和执行绑定，不调用 Teacher 偏好生成器。18 项合成检查通过，覆盖公共原链、实际 CLI、终端获取/保管，以及热缓存下篡改 replan/原生回执的拒绝；原 20 项控制链和动作编译回归仍通过。发现并修复共享商店回执形状门错误拒绝“已在站位、编译器省略移动”的合法交互。未启动游戏、未增加原生或训练分子，完整编排仍待接续，见 [合同](control-execution-admission.md#native-support-rebind)。
- **2026-11-04 控制执行与监督准入已分离：** rolling 获取选择从完整分母和原始输入独立重建，复用原子提交、当前候选、动作编译、执行绑定和 canonical settlement；控制队列不冒充 Teacher，合法原生结果可保管产物但偏好训练标记仍为 false。新增 20 项具名检查通过，覆盖公共文件入口、实际无 Teacher 参数 CLI、真实编译器/账本服务、结果与保管结算的独立重放，以及伪造标记/错提案/错路线/无产出拒绝；完整旧 collection Teacher、hermetic critical paths、rolling 25 和阈值 30 回归均通过，Release 零警告错误。全部为合成/无游戏证据，未新增原生运行或提升覆盖。范围与未完成项见 [准入合同](control-execution-admission.md)。
- **获取产物保管/捐赠核销已接原链：** Museum 获取的 canonical settlement 从独立重建的 binding/fresh gain 和新鲜物资图导出一件保管；原 route settlement 同修订号完成输入并调用既有 portfolio commit 保管产物。捐赠候选/编译/派发只允许有类型和创建历史、目标匹配的精确消费者授权，普通改写/取消不能绕过；live 钩子复用 supporting-transition 核销，验证实际背包减一和 museum 精确完成，只读取已摄取快照的三组字段。Backend `303/303`、Core 聚焦 `87/87`、rolling `25/25`、完整阈值 `30/30` 和 Teacher 聚焦重放通过。以上是合成/进程内证据，未运行游戏、未提升覆盖或正式训练；网络拒绝后的回执恢复和保管位置迁移尚未闭合。详见 [保管合同](museum-output-custody.md)。
- **真实 rolling 提案生产器已实现：** `build-rusty-key-rolling-supply` 从完整 museum 分母和当前状态出发，优先把安全已有库存接入原候选/时间轴/日计划/动作编译链；否则按当前日完整获取上下文逐项调用原 portfolio/atomic preflight，产生单项普通提案。今天只有一件可用不再要求先凑齐所有缺口，完整阈值证书仍严格要求全部缺口。控制排序不冒充 Teacher 偏好，生产器目前是 CLI，尚未接成自主 live root 的供给/提交编排。24 项合成回归、原 30 项阈值回归及独立 Teacher 聚焦重放通过；真实 CLI 导出 pending 捐赠队列，拒绝时既有选中队列 SHA 不变；本轮不启动游戏、不提升覆盖。见 [rolling 合同](rusty-key-rolling-supply.md)。
- **PR #398 审查修复：** 本地 Teacher/导入队列不会自动进入 Backend 队列表，原按 ID 的派发检查会返回 404；现 live loop 向同一派发路由 POST 即将执行的队列项，GET/POST 复用一个当前快照/真实账本校验路径，不另存队列、不重选动作，也不绕过预留门。真实 HTTP/live 方法回归覆盖未注册队列、账本变更和旧副本不替代实际项；Backend `279/279` 通过。原生证据仍仅绑定下述 `2e24b81b`，后续修复不冒充同一原生运行。
- **本轮捐赠链进展：** PR #397 已合入 `main@621f8fb2`。博物馆本地/远程候选和编译器现复用同一材料投影扣除其他目标预留，并经已有派发门复查账本版本、角色和状态；一栈有富余一件仍可捐，不全栈封禁。`2e24b81b` 在隐藏静音 E 盘边界档完成 7 队列 / 9 verified native action、59→60、正常跨日和事件 66/实际钥匙；原档 SHA 未变、进程端口已回收，离线复核 34 项通过。后续 Teacher 账本来源/哈希与独立重放已接通，另以 hermetic/入口测试验证，不能拼接成新的同版本原生轨迹。详见 [捐赠材料准入合同](museum-donation-material-admission.md)。
- **当前唯一接续：** 使用上方已接通的受控 rolling 供给/原子提交/执行绑定入口，取得同一版本的原生“获取→保管→捐赠→核销”轨迹；受控脚本入口不等于 MOD 默认 root 已集成，不把代码/合成编排当成完整 root 原生运行。补失败回执恢复与保管位置迁移，再验证联合时间/开馆窗口、次晨交接及本 criterion 的独立比较/原生三分区、第三年前时限。不另建 planner/executor，不把单项提案冒充完整阈值证书，不要求今天所有 60 件都有路线。既有原生运行仍为 Year 5 共享能力，`5/19、6/19、5/19、4/11` 和正式训练状态不变。
- **隔离配置纠正：** 新测试发现 `LiveTrainingOptions` 构造时默认读取 E 盘历史 manifest，会绕过显式 Root；现仅无显式身份的 CLI 保留旧恢复默认值，库构造和显式 root/run 不再隐式接旧任务。测试另固定临时 manifest 并断言路径边界。误落点仅两份本轮新增 mock ledger，负例改坏的一份已恢复为哈希匹配内容，旧存档/manifest/训练文件未改，未擅自删除文件。

下方阈值组合与 #394 的开工/提交前条目为历史；当前进展与接续以上方为准。

- **当前接续：** #394 已由 PR #396 合入 `main@085f95a1` 并关闭，远端 CI 和同 SHA 自动审查通过。现在继续同一 Rusty Key primary，不重开其他根。新 `build-rusty-key-acquisition-portfolio` 复用共享组合/预留预检，保留完整博物馆分母，要求所选不同未捐物品恰好覆盖实时缺口；不额外要求 95 件全收集。阈值组合聚焦正反例已通过，具体语义和后续缺口见 [阈值组合合同](rusty-key-acquisition-portfolio.md)。本项未增加原生运行或 coverage 分子。
- **剩余边界没有隐藏：** 当前证明供给组合的阈值/资源准入，不是联合执行时序、产物保管至捐赠、最优 Teacher 选择或实际取钥匙。下一步沿既有 rolling execution/reservation 接到精确捐赠候选，补联合时间/开放窗口与次晨交接，再闭合本 criterion 的独立比较/原生三分区和第三年前时限。此前 `collection-replay-v4` 的访问错误在单次带诊断的完整 collection 重放中未复现（退出 0）；旧失败保留，不能声称已定位或修复其根因。

下方 #394 提交前状态保留为历史；当前合入事实以上方为准。

- **#394 监督边界修复已实现：** dataset/trainer 共用 v2 typed admission，Backend、两个 CLI 与实时训练方法均接到现有 collection Teacher 独立重放器。保留完整候选分母，只消费重建验证过的显式 strict pair；`Selected`、原生成功、单候选、未比较或未准入候选不能生成正负对。无证据/篡改输入不能覆盖 checkpoint/manifest，旧 checkpoint 不自动升级。细节、入口和验收范围见 [监督准入合同](independent-teacher-pairwise-admission.md)。本地入口回归与聚焦重建验证已通过，合入以对应 PR 的 CI 为准；正式训练锁和覆盖计数不变。
- **验证范围与接续：** 新聚焦入口 `self-test-current-stage-one-policy-supervision` 复用原静态输入和 current-frontier/rod 检查，约 4 秒通过，不再借整套机器/作物/portfolio 长矩阵验证本监督边界。较宽的 `collection-replay-v4` 在进入本项前遇到 portfolio-continuation 夹具文件访问错误，未计通过、未删除；该诊断随下一共享 acquisition/reservation 工作复核，不把环境/夹具错误宣称为本项成功。#394 完成后直接回到下条 Rusty Key 安全组合，不重排战略 root。
- **提交前最终回归：** 无游戏 Core governance `1041/1041`、Backend `265/265`、监督边界聚焦 `35/35` 均通过；Backend 覆盖真实 HTTP 入口、两个 CLI 进程和 live loop 的实际训练方法，拒绝时核对既有工件不变。聚焦 Teacher 重建包含有效显式 pair、单候选仅保留证据和重算哈希后的篡改反例。尚待对应 PR 的远端 CI，不将夹具训练写作正式开训。

以下两条为 #394 开工前的核实与退出合同，修复状态以上方及当前源码为准：

- **本批次已收口；接续先处理共享正确性缺陷 #394：** PR #395 已合并到 `main@3d798867`，远端治理 `1006/1006`、Backend `259/259`、无游戏证据行为检查 `31` 项通过，增强捕获链离线复核 `34` 项通过。#393 的全部 R1 失效标注也已同步保留。新 issue [#394](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/394) 已由当前源码确认：dataset validator 允许无 TeacherSupervision，trainer 将 Student `Selected` 对所有其余可用 admitted 候选直接生成偏好对，Backend 与 live loop/CLI 可达。修复尚未完成，不关闭 issue、不放开正式训练锁；本轮没有训练模型或更新 checkpoint。
- **#394 的实现边界和退出要求：** 复用 collection Teacher 已有的 `pairwise_preferences` 及独立重建/原生回执验证，在 dataset/trainer 共用 typed admission 中只消费严格绑定来源、状态、完整候选分母、分区和版本的显式比较；不能仅补 provenance 字符串。Student 观察、native outcome 与 Teacher preference 保持分离；tie/incomparable 不因未被执行而变成负例。覆盖 dataset builder、trainer、Backend、live loop/CLI，验证无监督或篡改输入不能写 checkpoint/正式 manifest，合法独立比较能训练。修复通过后直接回到下条安全 acquisition/reservation 阈值组合；这是同一 primary 的共享正确性插入项，不重排战略 root。

- **19:11 原生边界链已通过，未晋升 criterion：** PR #392 已合并；`ba9bb50e` 在隐藏静音的 E 盘游戏上，从复制的 59 件边界档完成进入博物馆、捐第 60 件、正常回家保存跨日、Farm 原生事件 `66` 和实际钥匙回执，共 7 次选队列、9 条 verified native action。源档目录 SHA-256 前后相同，游戏和专用端口均已回收。证据是共享原生能力：源档为 Year 5，不证明第三年前获得钥匙、60 件安全收集组合或 Teacher/native 三分区；全局计数保持冻结。详见 [Rusty Key 原生阈值证据](rusty-key-native-threshold-evidence.md)。
- **当前下一步：** 复用已有 museum requirement inventory、acquisition portfolio 与 reservation ledger，把目标限定为 `max(0, live_threshold - donated_count)` 个不重复、未捐且满足跨目标保护的物品；不要求先完成 95 件、不另建捐赠/剧情执行器。随后接入本 criterion 的 Teacher comparison、native outcome 三分区与第三年前 deadline。已有原生边界链只需独立重放和真实回归时重开，不重复整条游戏长跑。
- **离线读取修复：** 新证据复核暴露公共 PowerShell `Read-RuntimeLoopArtifacts` 把内容寻址快照清单当成完整 JSON；现在与 Full Shipment 共用 `RuntimeLogicalJson.ps1`，交由既有 C# reader 校验并还原，不手工拼快照。当前捕获链已用增强校验离线复核通过。

以下为本次原生验证前的实现记录；其中“下一步 59→60”已由上方结果完成：

- 当前 reconciliation 将本 root 保留为 `pending_dependency_expansion`：仍缺受保护物品安全的 60 件 acquisition/reservation/donation 组合，以及原生奖励到送钥匙事件的完整回执；Teacher source adapter 与三分区也尚未闭合。现有 EVD-224 捐赠、EVD-322 剧情和共享下水道执行器继续复用。
- 已补齐同一方向的两处绑定：博物馆远程候选携带与店内捐赠共用的精确阈值参数，仍走共享 connector；自动剧情候选只有在可读博物馆奖励前置成立、原生 `Data/Events/Farm` 事件 `66` 与 fresh fingerprint 一致时，才可绑定 `obtain_rusty_key` 并交给已有剧情编译器。缺失/错误类型的必需布尔字段、错误剧情来源和过期/重复指纹均拒绝。
- 锁定原始数据 `museum60` 的奖励动作是 `MarkEventSeen Host 295672`；Farm key `66/e 295672/t 600 700/H` 包含原生 `rustyKey`，反编译 `Event.DefaultCommands.RustyKey` 才写入实际钥匙状态。捐满 60 件不能直接声称拿到钥匙。
- 验证：Core Release `0 warning / 0 error`；方向绑定、博物馆、剧情与 catalog 聚焦回归 `118/118`。新增反例曾发现缺失布尔值被默认 false 放行，现已修复。无游戏模式聚焦 `71/71`、完整无游戏测试 `1006/1006` 通过；共用参数 helper 已移入共享夹具 partial，新方向回归的 CI 编译清单可独立构建；本次未启动游戏，未增加任何 coverage 或训练分子。
- 下一步：复用隔离 E 盘测试入口，验证 `59→60` 原生捐赠、奖励前置、正常跨日/农场入口和送钥匙实际回执，显式标记边界夹具起点；随后补共享安全 acquisition/reservation 的阈值组合及本 criterion 的独立 Teacher/native 分区。继续 WIP=1，局部验收按 R2，不要求先完成 95 件 Museum 或完整 Day-0 日程。

> **当前接续决定（2026-11-03T18:30:00+08:00，用户确认）：** #390 / PR #391 已完成验收责任解耦；当前唯一 primary 为 `obtain_rusty_key`。先完成安全捐赠阈值、原生奖励与送钥匙事件的局部闭环，再补本 criterion 的 Teacher/native 分区。R2 的四层验收合同继续生效；R1 中 Master Angler 固定第 10、强制 Gourmand/source-schedule 及完整新档反向前置均已撤销。后续未开工 root 的顺位待当前项收口后按真实局部缺口决定。Pet Love、Skull Key 已消费的严格验收保持冻结，原始中间文件清理不触发重采；当前计数仍为 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`，`formal_product_training=false`。

> **当前唯一 WIP（2026-11-03，#390 已收口）：** PR #391 已合并，#390 已关闭；Master Angler criterion-local、共享能力证书、Teacher 全局集成和完整新档四层责任已解耦，本次用户确认下一 primary 为 Rusty Key，其余顺位按 R2 待重估。当前覆盖保持 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`，正式训练仍为 false；Pet Love 与 Skull Key 的严格验证结论已冻结，后续原始中间文件清理不触发重采。按用户授权顺序，唯一 primary 是 `obtain_rusty_key`：先重建该 root 的当前 reconciliation/blocker/evidence reuse，再闭合安全的 60 件 acquisition/reservation/donation 组合、原生奖励/钥匙和下水道入口，不启动 Day-0 长跑。

> **当前状态入口：** [CURRENT_STATUS_CN.md](CURRENT_STATUS_CN.md)。本文件下方带日期的状态、计数和“下一步”均保留为历史证据；若与当前机器报告或 CURRENT_STATUS_CN.md 冲突，不得按历史数字规划新工作。
>
<!-- audit_invalidation: scope=r1_top; invalidated_at=2026-11-03T16:30:45+08:00; annotated_at=2026-11-03T18:52:51+08:00; superseded_by=PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md; historical_only=true -->
> **R1 失效时间戳（2026-11-03T16:30:45+08:00）：** 15:46:34 的旧开发全序、Master Angler 固定第 10、将完整 source schedule 与 Gourmand 3/3 作为局部必要条件的指令已被 R2 撤销，仅供历史追溯，不可据此指导 Teacher、生成标签或改变局部准入。当前 Rusty Key primary 另经用户 18:30 确认，继续有效；后续尚未开工 root 仍待重评。WIP=1、严格原生回执、Money 完整 Exit Gate 与 formal training 禁入均不变。

> **历史 R1 施工顺序（2026-11-03T15:46:34+08:00，Asia/Kuala_Lumpur；pretraining-order-20261103-r1）：** Community Center、Pet Love、Skull Key、Full Shipment 保持已冻结；后续顺序改为 `obtain_rusty_key` → `raise_skill_levels` → `raise_friendships` → `marriage_and_house_upgrade` → `complete_museum_collection` → `complete_master_angler` → `earn_money`。当前唯一 primary root 改为 `obtain_rusty_key`；Master Angler 保留成果、暂停在未完成状态，不再占据第 5 项。本次为用户明确授权的依赖/成本重排，不是普通 bug 中断；仍为 WIP=1。完整依据、退出条件与历史队列见 [CURRENT_STATUS_CN.md 第 6 节](CURRENT_STATUS_CN.md#6-下一项如何选择)。

<!-- pretraining-order-20261103-r1; edited_at=2026-11-03T15:46:34+08:00; research_base=049f40edfcef910b00451ad5f7b948916df67ba1; owner-authorized; docs-only -->

<!-- audit_invalidation: scope=r1_section; invalidated_at=2026-11-03T16:30:45+08:00; annotated_at=2026-11-03T18:52:51+08:00; superseded_by=PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md; historical_only=true -->
> **局部失效说明：** 本段固定全序以全局证明错误倒挂单个 Master Angler root 为成本依据，已失效。R2 将证据分为 L1 criterion-local、L2 共享证书、L3 全局集成、L4 新档评测；Gourmand 只是核桃 OR 方案。这里的历史任务条目不能代替顶部 18:30 当前 primary 决定。

## 2026-11-03 训练前开发顺序修订（历史 R1，验收与顺位以顶部接续决定及 R2 为准）

**时间：2026-11-03T15:46:34+08:00（Asia/Kuala_Lumpur）；研究基线：`049f40edfcef910b00451ad5f7b948916df67ba1`。**
用户明确授权纠正依赖倒置和难度倒挂；本节替代旧“Master Angler 当前第 5 项”的调度指令，原运行/测试记录保留为历史。

前四项 Community Center / Pet Love / Skull Key / Full Shipment 保持冻结。后续为：

`obtain_rusty_key` → `raise_skill_levels` → `raise_friendships` → `marriage_and_house_upgrade` → `complete_museum_collection` → `complete_master_angler` → `earn_money`

**唯一 primary：`obtain_rusty_key`。** Master Angler 保留已有实现和证据，显式暂停而非完成；Money 的最终 18/18 门仍在最后，其有界供给仍可供前面各项复用。
理由与源码索引统一见 [CURRENT_STATUS_CN.md 第 6 节](CURRENT_STATUS_CN.md#6-下一项如何选择)：下水道复用未闭合 Rusty Key，捕鱼/天气复用技能生产者，婚姻复用社交；v111 又证明“先跑昂贵新档、后补姜岛边界 smoke”的验收次序需要修正。中间相对工期属工程估计，不冒充已测量成本或新增游戏硬依赖。

验证顺序固定为：**锁定源码/旧证据复核 → 隔离边界原生 smoke → 同版本短链和组合验证 → 本 root 所需完整新档/分区验收**。边界档不能冒充从零解锁；改变二进制或丢失未保存内存 continuation 后不得拼接。已有完成项只对真实回归/authority 漂移重开。

本次为文档重排，未运行游戏或生成新证据。`5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready、26/26 Full Shipment、0/18 Money production、formal_product_training=false` 保持不变。继续 `--skip-training`，全局正式覆盖、Provider/模型定型与产品 Stage 1/2/D 顺序不变；#381 owner PASS 不重做。

### 本轮接棒任务：Rusty Key，不另开岛屿长跑

1. 先按当前 source 重建 Rusty Key 的 reconciliation/blocker/evidence-reuse；图节点或执行器已存在不代表 root 正式通过，不重复新建候选、捐赠或开锁执行器。
2. 把工作范围限制为受保护物品安全的 60 件 acquisition/reservation/donation 组合、原生阈值奖励和现有下水道入口。先验证阈值前后的真实奖励/控制交接，再补本 root 缺失的生产证据与三分区；不把 95 件 Museum 当先决条件。
3. 达到既定 Exit Gate 后冻结，再进入 Skills；当前只是计划接棒，不声称已经开始或完成这些步骤。
4. 两个常驻对话平级且各自可调用子代理，但实现、交叉审查、采证都服务同一 primary；不得借本次重排同时开 Skills、Museum、Money。

<!-- audit_invalidation: scope=r1_angler_retention; invalidated_at=2026-11-03T16:30:45+08:00; annotated_at=2026-11-03T18:52:51+08:00; superseded_by=PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md; historical_only=true -->
> **历史第 10 顺位已失效：** 已解锁岛屿副本只证明已声明前置下的局部能力，不得将完整 day-zero/Gourmand 反向列为局部门。

### Master Angler 保留点

v111 已人工收口；PR #389 的隔离已解锁姜岛基线和两份 SHA-256 保留。它对应的 boundary smoke 作为第 10 项重新开工的首门，而不是删除或伪造成功。恢复时先验证岛屿捕鱼地点/Gourmand/核桃/原生回执，再在上游与局部能力齐备时做全新 day-zero 终局证明。不得续接 v111 内存态，不触碰用户游玩存档。


## 2026-11-01：v98 已按新预算边界从全新 day-zero 启动

- 唯一原生入口 `golden-walnut-source-schedule-v98-20261101-225447` 已由 `main@274471f0` 启动：隐藏静音、单并发、`skip-training`，同时要求 source schedule 与 Gourmand continuation；存档槽为全新 `ProofFarm_453221812`，没有续用 v97 状态。
- fresh-save 与三项产品服务已通过。iteration `4` 已记录 `3` 条 verified Product action 且无错误；iteration `2` 形成 `84` 动作的 crop teacher 原生计划，后续按真实库存约束进入 `acquire_seed`。当前 source milestone 与三段 Gourmand 仍处于开局等待态，属于预期未达成条件而非卡死。
- v98 已真实完成 6 粒花椰菜种子的逐次购买，资金 `500 -> 20`，并于 iteration `43`、verified action `42` 进入 `prepare_site`。iteration `36` 的三项购买队列因每项都采集 `full` 快照耗时约 3 分钟；当前源码已沿用现有 execution profile override，把 seed/route/crop batch/sleep/dialogue recovery 收窄到 `shop/route/daily`，场地准备仍用 `farm_layout`，而全局 Golden Walnut 基线和最终证明仍保持 `full`。相关 LiveTrainingLoop 回归 `38/38` 通过；运行中的 v98 使用冻结二进制，不热替换该修复。
- v98 已进一步完成首个真实作物控制闭环。原 6 格花椰菜在一格环境损失后严格触发 `crop_teacher_water_target_crop_missing`，冻结旧 ledger 并重规划 8 格防风草；新承诺完成场地准备、补水与 4 条原生日界，在 iteration `115` 取得 `8/8` 原生收获回执，iteration `116` 以 `111` 条 verified Product action记录 `crop_teacher_run_complete`。iteration `117` 已自然切回 Stage One collection 并执行邮件 Teacher 队列，证明共享日计划没有被作物链锁死。旧地块剩余 5 格花椰菜尚未成熟且未浇水，继续观察共享候选是否接管，不在无失败证据时并行增加作物调度实现。
- iteration `125` 后的 Egg Festival 路线等待由透明时序明确约束为当日 `14:10` 重新开放，不是无界死循环；运行中的 v98 正按 `600` tick 原生片段持有同一机械 continuation，且每段都有 fresh snapshot。现场同时暴露旧日计划会在第一条滚动等待后预先保留另外两条过期候选；`main@89e84be6` 现把首条 deferred wait 设为 fresh-replan barrier，后续候选必须等待新快照重新排名。日计划编译回归 `42/42` 通过；v98 使用冻结旧二进制，只作为修复前集成证据，不热替换。
- v98 已在 iteration `167` 原生越过 `14:10`，证明上述等待确有有限终点。Community Center 候选随后因 `community_center_junimo_text_not_readable` 正确失效；iteration `169` 则重新规划并真实执行 Beach -> Town -> BusStop -> Farm -> 出货箱站位 -> 原生投递的完整五步队列，鱼 `(O)147` 已从库存移除。冻结旧二进制仍把这次成功误判为未完成，因为 continuation 保存的旧站位 `70` 与 fresh 路径重绑站位 `73` 不同；`main@7d143882` 已把站位从出货目标完成身份中移除，但仍严格校验物品、槽位、数量、单价和出货箱坐标。同期 `main@397b4216`、`54f79dd6` 让精确开放时刻使用至多三段的有界原生等待，并把全部授权片段计入时间预算；相关组合回归 `118/118` 通过。上述新代码尚未由 v98 实机复验，v98 仍只提供修复前证据。
- iteration `177/179` 的 full Farm 快照提供了此前缺少的共享维护反例：5 格旧花椰菜全部为 `needs_watering=true`，但 Stage One 的 98 个收集候选没有任何作物维护项，捕鱼/清障仍继续执行。根因不是缺少浇水候选或执行器，而是 Stage One 入口只在自身候选为空时才让出共享日计划。`main@df03f722` 现只对透明 `current_location.crops` 中当天需浇水的义务执行抢占，把该轮严格限定到既有 `farm.maintain_crops -> water_crop_tile` 日计划链；活动战略 continuation 在维护执行期间显式挂起并于结束后原样恢复。Farm 单例、收获、播种和施肥不会触发这条浇水抢占，因此没有第二套作物规划或动作实现。聚焦编译/回归 `109/109` 通过；冻结 v98 仍不能实机验证此修复。
- iteration `195` 的 Beach 出货恢复又给出连接器预算反例：透明碰撞网格和运行时使用同一个原生 `isCollidingPosition` 口径，`(44,25) -> (38,0)` 的快照最短路精确为 `31` 格，蛤蜊等对象也已作为阻塞格进入网格；缺口不是字段遗漏。旧编译器只授权 `32` 格，快照生成到执行期间 NPC 或临时占位变化即可耗尽这一格余量并返回 `movement_no_collision_safe_path`。当前源码集中使用连接器动态占位预算，最短路外再保留至多 `16` 格有界绕行空间，并把相同上限计入 `estimated_ticks/estimated_minutes`；这三个编译派生参数每次都覆盖候选携带的旧值，不能由陈旧计划压过 fresh 快照。海滩复现预算由 `32` 刷新为 `47`，连接器聚焦回归通过，全解决方案构建 `0 warning / 0 error`；冻结 v98 尚未加载该修复，不能作为实机通过证据。
- v98 已在 iteration `202` 按连续 `8` 次未验证执行硬门停止，所有进程和端口均已回收。最终留下 `173` 条 verified Product action、`293` 个耐久 fresh-save episode；停止原因只指向上述 `executor.traverse_connector` 路径预算，source schedule 仍因首次登岛、来源增量和三项鹦鹉工程未观察而不完整，Gourmand 为 `0/3`。v98 不得续接或进入正式 corpus；下一运行必须使用最新主线和全新存档重新验证等待、出货完成身份、浇水抢占与连接器动态绕行。
- v99 首次启动身份 `golden-walnut-source-schedule-v99-20261102-005523` 在任何游戏进程启动前因长链 Windows PowerShell 会话无法解析 `Get-FileHash` 而退出，没有有效新档或训练数据。公共部署守卫现改用纯 .NET SHA-256，同时保留复制后源/目标哈希相等的失败关闭；源守卫 `3/3`，Windows PowerShell 下 Bridge+Harness 与 pwsh 下顺序部署均通过。有效运行身份是 `golden-walnut-source-schedule-v99r2-20261102-005859`，新存档 `ProofFarm_453229167` 已通过 1.6.15 原生门并进入单并发动作循环，stderr 为空。它继续使用 `skip-training`、source schedule 与 Gourmand 双硬门，只验证当前源码并推进同一主线，不继承 v98 内存态或诊断尾部。
- v99r2 在 crop teacher 的真实购种阶段连续复现 `profile=shop` 被 Backend 以 `unsupported snapshot profile` 拒绝。Bridge 已返回预期的十个目的域，缺口是 Backend 校验表没有登记同名目的配置，不是商店字段缺失。该轮在 iteration `6` 人工停止并完整回收进程，禁止继续产生重复错误。当前源码增加严格 `ShopRequiredDomains`：十域完整时接受，缺少 `npcs` 等任一必需域时仍失败关闭；专项 `8/8`、Backend `258/258`、全解决方案构建 `0 warning / 0 error`。下一原生入口必须使用新二进制和新 run 身份复验购种链，不得热替换或续接 v99r2 进程。
- v100 `golden-walnut-source-schedule-v100-20261102-010715` 已加载上述 shop 修复并通过 fresh-save 启动，但只到 iteration `1` 的正常 source/Gourmand 等待态便主动停止。启动间隙的源码一致性审计证明 crop teacher 购种后的下一控制步会使用 `profile=route`，而 Backend 同样尚未登记；Bridge 的 route 与 shop 都发布相同十域合同，因此继续等待实机重复同类 422 没有信息收益。当前 Backend 统一使用严格 `RouteAndShopRequiredDomains`，两种 profile 均验证完整接受与缺域拒绝；专项 `9/9`、全解决方案构建零警告零错误。v100 不作为完成证据，下一轮从新身份同时复验购种和购后路线。
- v101 `golden-walnut-source-schedule-v101-20261102-011200` 已用全新 `ProofFarm_453229946` 实机穿过上述两项注册修复：开店前的 shop 快照、有界等待和逐粒购买均正常，6 粒种子全部购得；iteration `24` 明确切到 `route_to_farm`，route 快照成功入库并追加 verified Product action。该点累计 `23` 条 verified action，`stage=error=0`、`unsupported snapshot profile=0`。运行继续使用同一隔离二进制进入场地准备和后续原生作物闭环；该证据只提升目的快照兼容性结论，不提升 source schedule、Gourmand 或正式训练分子。
- v101 的首轮 crop teacher 已进一步原生闭合：场地准备、种植/浇水、`12` 条已验证日界和 `6/6` 收获完成，iteration `76` 写入 `crop_teacher_run_complete` 并交还共享 Stage One；随后处理邮件、推进 quest 路线并在 Beach 完成一次 Master Angler 捕鱼。第二轮 teacher 从 Beach 回农场时遇到春 13 日节日门禁，既有 `route_wait_max_slices=3` 每个回执实际推进约 `1800` tick，并在透明开放时刻 `14:10` 后退出。iteration `94/95/96` 的 `Beach -> Town -> BusStop -> Farm` 三个 `executor.traverse_connector` 均为 `status=applied`，实机通过了 v98 曾失败的 Beach 出口方向；iteration `97` 已进入第二轮场地准备。到该点 `stage=error=0`，但 source schedule/Gourmand/正式训练分子仍不变。
- 第二轮 8 格防风草也已在同一 v101 身份完成。成熟日首先因不同品质与满背包只收获 `1/8`，既有容量恢复随后分两次释放槽位，剩余收获按 `4+3` 收敛为 `8/8`，iteration `126` 写入第二个 `crop_teacher_run_complete`；没有跳过原生收获或伪造可用容量。Stage One 随后释放两个非保护库存栈、完成 `(O)708` 的 Beach 捕鱼并按精确最新捕获单例恢复容量，再切换到 `(O)145` 的 Town 路线。iteration `133` 前累计 `126` 条 verified action，仍无 `stage=error` 或停止信号。
- v101 后续把同一 Master Angler 高层目标从 Farm 经 BusStop 重绑到 Town，证明 route candidate ID 随 fresh 起点变化不是失锁原因；Town fresh availability 精确给出的唯一阻塞是 `fishing_inventory_full_requires_storage_transfer`。旧控制顺序却先按 active continuation 裁掉所有出货候选，之后才调用已经存在的 `StageOneInventoryCapacityRecovery`，所以只能释放并整日抑制捕鱼目标，下一轮再无目标地出货。当前源码改为先在未裁剪排名上选择目标安全的容量恢复，再保留原捕鱼 continuation；恢复仍走唯一出货候选、DailyPlanCompiler、ActionQueueCompiler 和 Product Executor，不新增规划或执行路径。
- 该顺序修复的 queue-replan/容量恢复聚焦回归为 `75/75`，全解决方案 Release 构建为 `0 warning / 0 error`。v101 使用启动时封存的旧二进制，已在 iteration `136/142/146/151/157` 重复提供修复前反例；iteration `158` 的精确最新捕获单例出货成功追加后人工安全停止，最终累计 `141` 条 verified action、`stage=error=0`，产品日志无待提交项，全部子进程和专用端口已回收。该运行不会热替换，也不得续接为当前源码证据。下一新身份必须原生证明满包时在 active fishing continuation 下直接编译 `ready_inventory_capacity_recovery`、恢复后继续同一捕鱼目标且不写 `suppressed_until_day_change=true`。
- v102 首个启动身份 `golden-walnut-source-schedule-v102-20261102-022835` 在 fresh-save 之前取消，原因是历史根预算器用 PowerShell 对象递归扫描 23 个根，数分钟没有完成；运行根、游戏与证据均未创建。预算器现用只读 `robocopy /L /S /BYTES /XJ` 取得相同文件数和逻辑字节，不复制、不删除、不跟随 junction。v101 精确交叉校验为 `10615` 文件和 `1133107181` 字节；23 根总计 `9690 MiB`，第 24 根仍在既有门内，完整测量约 `29` 秒，AST 与 source guard `9/9` 通过。下一启动使用新 run identity，不能沿用空身份。
- 有效运行 `golden-walnut-source-schedule-v102r2-20261102-023649` 已完成首轮 crop teacher 并于 iteration `90` 交还 Stage One。iteration `95` 的 Beach 捕鱼队列在满包 fresh 快照失效；iteration `97` 真实写出 `continuation_released / suppressed_until_day_change=true`，因此 v102 已成为修复前反例。排名证据把缺口进一步缩到 Egg Festival `06:50` 的时间门：出货路线今天合法但要到 `14:10` 才开放，旧恢复选择器因 `Available=false` 拒绝全部 deferred 候选。游戏、Backend、ProductExecutor、LiveTrainingLoop 和启动器于 `03:15` 原地挂起且 CPU 增量归零；自动续跑已取消，loop 已在 iteration `98` 落盘后的迭代边界停止。应用户的一小时静音要求，父脚本 finally 清理延后到 `04:45`，容量修复的聚焦测试与构建最早于 `04:49` 启动。独立分支 `fix/v102-deferred-capacity-recovery` 将复用现有 timeline scheduler 与 DailyPlanCompiler：仅当出货路线唯一阻塞为 `connector_active_festival_transit_closed` 时选择安全物品并编译 600 tick 滚动等待，继续保留捕鱼 objective；保护物品和其他阻塞继续 fail-closed。选择次序固定为“立即同物品、立即安全物品、延迟同物品、延迟安全物品”，不会为了等精确捕获物的节日通道而忽略当前即可释放的安全槽位。战略捕鱼 continuation 在恢复期间停放于独立槽，恢复租约只刷新其自身 `economy_shipping` continuation；恢复完成或失效才复位捕鱼目标。Stage One 与 Crop Teacher 的机械容量恢复都不会提升全局 objective 分子或写入整日抑制。修复验证后必须用新运行身份证明等待期间不释放 continuation、开放后完成容量恢复并继续同一捕鱼目标。
- 代码级验证结果为聚焦回归 `121/121`、全解决方案 Release `0 warning / 0 error`。首次 clean-worktree 构建按计划使用 `--no-restore`，暴露九个未被聚焦测试还原的项目缺少 `project.assets.json`；补一次全解决方案 restore 后，同一 `--no-restore` 构建通过。原失败与重试证据均保留，不将环境准备缺口算成源码错误。提交前继续检查补丁边界和 `local-data`；合并后以全新 v103 身份做节日等待 -> 出货腾格 -> 恢复原捕鱼目标的原生证明。
- v103r3 已在 iteration `87` 真实选中 deferred festival-transit 容量恢复并释放 `(O)129`，随后 iteration `88-94` 保持有界等待，错误和整日抑制均为零。该轮按静音要求在 iteration `94` 边界终止；尚未观察恢复后重建原 Master Angler 目标，且不能跨进程拼接内存 continuation。因此下一次容量证明必须从全新运行身份完整重放“等待 -> 出货腾格 -> 原捕鱼目标恢复”，v103r3 只保留为前半段原生证据。
- #381 夜间复合事务已由 PR #384 合并到 `main@2bab1dc9` 并关闭：隐藏矩阵 `7/7 + 2/2 + 2/2`、核心治理 `939/939`、Backend `259/259`、专项 `85/85` 全部通过，owner 可见门为 `PASS`，精确 SHA 复审无发现。下方候选形成记录仅保留审计历史，不再是当前 WIP。
- 唯一下一原生步骤恢复为全新 day-zero 隔离运行：先在同一未中断身份内证明 Egg Festival 有界等待结束后完成容量释放、恢复原 Master Angler 捕鱼目标且不写整日抑制，再继续既定 Golden Walnut source schedule、独立重放、Gourmand `3/3` 和 `72/14/3` 收口。继续 `skip-training`；运行输出只落本地证据根，不提交 `local-data`。
- v104 `...165700` 已从全新存档正常执行到 iteration `92`，随后第二轮 Crop Teacher 的 9 分块 Farm 布局串行读取跨过正常游戏时刻边界，iteration `93-97` 均以 `planning_handoff_farm_layout_boundary_drifted` 严格失败并安全停止。候选修复没有删除 save/player/day/time 校验，而是先并发排队全部分块，使 Bridge 在同一游戏线程更新点统一采集；专项 `5/5`、GameFreeGovernance `939/939`、LiveTrainingLoop Release 零警告零错误。下一入口改为全新 v105，先原生穿过第二轮 handoff，再继续既定容量恢复与 source-schedule 出口；v104 不续接、不拼证据。
- v105 `...173812` 已证明真实 Bridge 可在同一更新点采集首轮 `9/9` Farm 布局分块，并以 `86` 个 verified 动作、零错误越过 v104 的失败迭代区间。容量恢复也在 iteration `88/91/94/99` 原生命中；但 iteration `97` 把 Backwoods 来源变体暂时不可用误记为整日抑制，下一轮却立即为同一 `(O)136` 找到 Mountain 来源，因此本轮严格失败并在 iteration `100` 停止。候选修复把第一次空来源降级为同鱼种、同目标日、同权威身份下的来源重选，执行新来源后恢复完整绑定；只有鱼种级仍为空才允许整日有界释放。专项 `2/2`、QueueReplanFilter `65/65`、GameFreeGovernance `939/939`、Release 构建零警告零错误。下一入口为全新 v106：先证明日志出现 `continuation_source_variant_replan ... suppressed_until_day_change=false`，随后同鱼等价来源被编译并 verified，且相邻记录不出现错误的 `suppressed_until_day_change=true`；通过后继续节日等待、source schedule 与 Gourmand 出口。
- `cce0c530` 的来源重选现已完成两种真实边界并推送。v106 `...183052` 证明放宽来源后若 Backwoods 无精确落点且 Mountain 已无法在 19:00 前到达，可以在下一轮合法进入鱼种级整日抑制；v107 `...194623` 则在 iteration `181-182` 证明仍有时间时会为同一 `(O)136` 从 Backwoods 改选 Mountain，并把连接器、终端移动和捕鱼三步全部原生 verified，随后把完整 `Mountain:2` 来源身份写回 active continuation。当前运行继续沿既定 WIP=1 推进 source schedule、独立重放、Gourmand `3/3` 与 `72/14/3` 收口；仍为 `skip-training`，不得把该局部通过写成正式训练准入。
- #381 是独立的 owner 可见夜间复合事务门。本地未推送分支 `fix/381-local-end-of-night-transaction` 已形成候选：同一事务可在现有 Sleep 宏遇到职业选择时 yield，再由唯一的 `skills.choose_profession` 计划/编译/执行链恢复；事务只有在原生 FarmEvent、全部普通/职业升级页、出货或独立保存页、真实保存、精确次日和新鲜状态下的正常控制动作全部闭合后才能完成。复合脚本现严格核对第 31 天 `SoundInTheNightEvent`、两个普通页、一个职业页、事件先于等级页且等级页先于 Shipping/Save、精确收入/成就和无出货分支；测试复用开关避免重复构建。降噪暂停期间完成的静态审查消除了 HTTP 线程读取 `Game1/Context` 的边界违规：安全次日状态在主线程捕获，后台线程只确认 verified 动作并落事务数据；终态 lineage 只附着一次，且夜间控制动作不能冒充次日正常工作。本次静态复核又发现实际 Stage One 预检会忽略已请求的 `skills.choose_profession`，而选通用 `recovery_close_menu` 代选职业；候选现改为职业页只选显式职业候选，显式候选不可用时失败关闭，不得回退到通用关菜单。该候选尚未完成最新构建与隐藏原生验证，明日必须运行未剪辑可见演示并取得 owner 明确 PASS；此前禁止 PR、合并、关闭 #381 或把候选写成已接纳。
- 静音窗口的后续复核补齐了跨层身份：每个原生执行请求/回执现携带选中高层 option，夜间事务同时校验该高层计划和实际 primitive；优先剧情中断显式标成 `story.advance_event`，不会借用被中断目标的身份。精确结算收入也改为睡眠夹具完成后读取透明出货箱的最终结算值。下一步仍是构建与聚焦测试，不改变 owner 可见门次序。
- 重新核对 #381 正文后，验收退出条件已扩为完整原生合法矩阵和四类负例；两次隐藏 smoke 不再被视为足够。先完成构建/聚焦回归，再用同一事务与现有 planner/compiler/executor 路径补齐矩阵编排；最后才运行 owner 可见未剪辑复合场景并请求明确 PASS。
- 矩阵编排的第一项代码任务已在本地候选完成：保留 Harness 中现有可组合夹具，解除普通隐藏场景对“职业=2 个普通页+第 31 天事件”的硬绑定，并把等待原生菜单、核对 transition 次序、确认真实保存、执行并绑定次日正常动作整理为所有合法场景共用的单一路径。七场景正向入口只改变输入组合与期望，不新建执行器；默认隐藏模式只产生自动回归证据，`-VisibleGame -OwnerVisualGate` 才按同一候选身份逐场景展示，且完整复合场景仍额外受 `-OwnerComposite` 严格约束。#381 正文要求 owner 看到各原生合法类别、overnight special event/CG 与独立 post-save story/cutscene handoff；实际遇到 minigame 时必须分类并有限处理，但不制造合成正例。不能把一轮复合或隐藏日志替代整套目视门。下一步在静音限制解除后先构建和跑聚焦回归，再运行隐藏正向矩阵；负例仍须使用同一事务观察器验证有限超时和 typed failure。
- 锁定 1.6.15 反编译进一步证明剧情 handoff 必须与终端保存栈分离：`PollForEndOfNewDaySync` 重置地点时 Save/Shipping 菜单仍活动，`resetLocalState` 因而不调用 `checkForEvents`。下一静态施工项不是复用合成 `debug.setup_story_event`，而是新增只建立前置条件的原生夹具和独立 smoke：真实保存后先保留夜间事务自己的正常动作证明，再经真实 Farm 入口触发数据事件 `63`，交回已有 `story.advance_event` 链；另一轮以 Cat 领养事件 `1590166` 到达 `catQuestion` 后失败关闭，禁止自动选择。两轮均不得直接构造 `Event`、调用 `startEvent`、写 `eventUp/eventsSeen`，且必须绑定与正向矩阵相同的候选身份。静音限制解除后仍先构建和跑聚焦回归，再执行隐藏矩阵与这些原生 handoff/负例。
- 上述夹具入口和单一路径 smoke 现已完成静态实现：`debug.prepare_native_story_handoff` 与旧合成入口分离；Kent 模式实时验证首个合法 Farm 数据事件为 `63`，Cat 模式实时验证 `Pet.TryGetData` 的 Farm/`1590166` 映射。反编译复核同时修正 Cat 夹具三项前置错误：`m 1000` 是累计收入至少 `1000g`；原日期睡醒落在不允许的周一；Spring 24 虽是允许的周三，却会先被花舞节事件抢占。现固定从 Spring 25 睡到无节日的周五 Spring 26。Full Shipment smoke 在真实保存后从透明路线投影精确绑定唯一 `FarmHouse -> Farm` 连接器，先用该正常动作关闭夜间事务，再让原生 Farm entry 启动剧情；Kent 复用现有 `story.advance_event` 完成，Cat 在 `catQuestion` 以无响应请求验证 `story_event_dialogue_response_mismatch` 且不改变问题/宠物/已看状态。`Invoke-RuntimeEndOfNightStoryHandoffMatrix.ps1` 只顺序调用这两种 profile 并校验 Git、脚本、二进制和部署 bundle 身份一致，不复制剧情执行器。源码守卫覆盖无 `new Event`、无 `startEvent`、无已看标记写入和无 `eventUp=true`。该实现仍未编译或实机运行；静音限制解除后下一步是构建、聚焦测试，再跑隐藏正向矩阵与原生 handoff 矩阵。
- 静音窗口继续完成三项不运行游戏的收口。第一，Stage One 强制菜单预检过去在显式职业/恢复候选不可用时返回 `null`，调用方会继续普通 Teacher 构建；现改为 `blocked_mandatory_menu_recovery_unavailable`，同时落排名和阻塞原因，彻底禁止通用关菜单或普通日计划接管职业页。第二，升级夹具不再只伪造 `newLevels`：实际技能等级、原生经验阈值、待升级队列和战斗职业生命效果保持一致，普通升级与职业升级使用不同技能；菜单仍由原版夜间流程构造。第三，事务证据文件名复用现有净化函数，外部 `run_id` 不能改变证据目录。以上仍处于静态候选，下一步顺序不变：解除静音后先构建和聚焦测试，再跑隐藏矩阵，最后才进入 owner 可见门。
- 同一静音窗口又收紧了次日首个正常动作的完成判定：高层计划必须来自现有权威能力注册表、具有 declared 或 partially-blocked-family candidate、无需玩家确认且明确允许 policy/autonomous 调用；底层 primitive 必须同时通过事务安全策略和现有 Product Executor capability catalog。任意伪造的 `executor.*` 字符串不能再把事务标成完成。该修改仍未构建或运行。
- 重新逐项对照 #381 验收矩阵时发现原七场景共用一个职业 ID，默认只证明 level 5。正向矩阵现要求一对同技能、同分支的合法 level-5/level-10 ID：无出货的“职业+剩余菜单”覆盖 level 5，出货+职业与完整复合场景覆盖 level 10；仍只调用现有 `skills.choose_profession` 链，不复制职业执行器。该修正尚未运行。
- #381 的负向运行编排现也已静态接线：薄入口 `Invoke-RuntimeEndOfNightNegativeMatrix.ps1` 依次运行未知 `QuestLog` 的分段超时和整体超时。夹具只在真实睡眠、保存和安全次日交接完成后启用；生产超时策略不变，现有 Sleep/菜单执行器不复制，负向证据固定不可训练。静音限制解除后的统一验证顺序改为：构建和聚焦回归 -> 隐藏正向矩阵 -> 隐藏 story handoff -> 隐藏负向矩阵；全部稳定并提交后再运行同一 commit 的 owner 可见矩阵。
- 构建前静态复核发现普通 `LevelUpMenu` 也会生成不可用的职业占位；Stage One 不能仅凭该占位拒绝普通菜单恢复。选择逻辑现以透明快照的 `is_profession_chooser` 为失败关闭权威：明确职业页缺可用候选时阻塞，普通升级页忽略不可用职业占位并复用既有恢复候选。聚焦回归同时覆盖两条分支。
- 构建前超时复核还移除了 ActiveSleep 独立的 `30000` tick 魔数：入睡前路径预算按 `path.Count * 90` 增加，确认睡眠后的总体上限直接复用事务政策的 36000 tick，避免合法前序菜单只因两套上限不一致而提前失败。
- 构建前最后一项原生事件边界复核补全了第 31 天婚礼冲突：锁定源码在 `Game1.weddingToday` 外还会遍历在线玩家，已婚且 `WeddingDate == Game1.Date` 时同样拒绝 FarmEvent。室友仪式虽不设置前一个标记，但新日流程会先把关系标为已婚，故不能从冲突判断中排除 roommate。Harness 现在只在真实冲突时失败关闭，不清除婚期或关系来制造地震；合同测试与运行合同同步锁定该边界。
- 低负载源码守卫复核发现合同测试仍寻找猫领养夹具旧版的直接累计收入赋值，会在正确实现上产生确定性假失败。测试现改为分别要求 `preparedTotalMoneyEarned` 的下限计算和 `projectedWakeTotalMoneyEarned` 的醒来态赋值；产品行为不变，待静音窗口结束后由聚焦测试确认。
- smoke 不再把 Harness 返回的证据路径本身当作成功：必须读取隔离 training-output 根内的真实事务 JSON，核对 schema/run/transaction/status，并把 SHA-256 写进正负摘要。`activeSleep` 退出早于负例 `QuestLog` 注入的顺序也已由源码守卫固定。下一步顺序仍是构建与聚焦测试，再依次运行隐藏正向、story handoff 和负向矩阵。
- 隐藏正向矩阵的首次逐场景运行已经把职业阻塞缩到原生 250ms 输入冷却：基线出货、空箱保存和普通升级曾分别通过，但职业页在 `timer_before_start=234 / can_receive_input=false` 时因 Harness 空闲暂停而不再推进；墙钟等待不是修复。当前静态候选在透明字段完整、信息面板已展开、两个职业身份精确且剩余计时器位于原生 `1..250ms` 时，只生成一个最多 30 tick 的 control wait，随后 fresh replan 选择显式职业；LiveTrainingLoop 不把该 wait 计作 verified action 或训练轨迹，即使等待恰好跨过游戏内时钟边界也不例外。预检也已改为保留 CLI 显式绑定的职业 candidate ID，冷却结束后若该 ID 不在原生报价中就失败关闭，不会退化为 baseline 偏好职业。旧通用恢复候选代选 baseline 职业的第二规划入口已经删除，编译层要求 `skills.choose_profession` 专用安全标记后才允许复用 `executor.close_menu` 的 LevelUpMenu 原生路径。下一步仍须先构建和聚焦回归，再用同一最新候选身份从头重跑全部隐藏矩阵，旧三轮 PASS 不可拼接。
- 同一静音窗口的符号复核发现职业等待候选误用了另一类的 `ActiveMenuType` 帮助函数，已更正为候选评估器现有的 `ActiveMenuTypeForCandidate`。该项是编译前静态修正，尚未运行构建，不改变上述验证次序或候选完成状态。
- 静音解除后已完成本地候选的代码门：受影响测试方法 `191/191`，全解决方案 Release `0 warning / 0 error`。扩大 partial-class 诊断为 `428/432`，四项失败不在本切片修改的方法中，故保留为仓库基线诊断而不冒充全绿。当前 smoke 脚本身份下，Kent 与 Cat 两个原生单例均通过；Kent 的真实事件 `63` 已在安全次日队列的优先中断中完成，旧脚本只是错过短暂活动窗口并在随后宠物事件上超时。现在必须消费同一聚合回执中精确绑定的原生完成证据，不启动第二个 Kent，也不压制宠物 fallback。
- 当前脏工作树的 story handoff matrix `2/2` 与 negative matrix `2/2` 已通过；较早正向 `7/7` 的脚本身份已被上述编排修正改变，不能拼接。当前唯一施工顺序为：提交候选并得到 clean commit -> 同一 commit 隐藏正向 `7/7` -> story `2/2` -> negative `2/2` -> owner 可见矩阵与精确 PASS 问题。提交后隐藏矩阵前不得再改代码或文档；任一矩阵失败时只修单个失败场景并重新建立候选身份。
- 运行证据只写入对应 `I:\StardewAITraining\golden-walnut-source-schedule-v*` 根，不进入 Git。后续低频观察只读取进度日志和终止证据；未达到 source schedule、独立重放、Gourmand 连续性及自动 completion proof 前，不得把本轮称作正式训练完成。

- **最新施工终态（2026-11-03）**：#381 的 owner 目视 `PASS` 仍是已消费终态，不得重复启动。Crop Teacher v6 上游选择修复已通过 PR #387 合并到 `main@047f9dcc`；hermetic Bootstrap、Crop Teacher `58/58`、GameFreeGovernance `939/939`、Backend `259/259` 和两个 Release 构建均已通过。
- 全新 day-zero v110r2 已完成三轮 Crop Teacher。第二轮的真实 v6 决策把补水站位绑定到 `(72,27)`，准备列表为空，不再包含 v109 的阻塞树桩；随后完成场地准备、`4` 条原生日界、`8/8` 收获并正常结束。第三轮经历一粒种子短缺、优先剧情中断、补水与容量恢复后，同样完成 `4` 条日界和 `8/8` 收获。该轮夜间停止前到达 iteration `209`、`162` 条 verified Product action，`stage=error=0`、三个 stderr 为零，全部进程和专用端口已回收。
- 当前唯一 WIP=1 已回到 Golden Walnut/source-schedule 主线。v110r2 未首次登岛、未产生核桃来源增量、未完成 Gourmand 连续性，也不能跨停止后的内存边界续接；下一入口必须是最新 `main` 上新的 day-zero 身份，继续首次登岛、First Parrot、Western Turtle、Island House、source schedule 冻结/独立重放、Gourmand `3/3` 与 `72/14/3` completion proof。全过程继续 `skip-training`、`formal_product_training=false`，运行输出只落本地证据根，禁止提交 `local-data`。
- v111（`golden-walnut-source-schedule-v111-20261103-091730`，`main@9974d32e`）已从全新 day-zero 存档运行到 Year 1 Spring 27；人工收口时为 iteration `647`、`479` 条 verified Product action、`stage=error=0`，所有进程和专用端口均已回收。该轮没有首次登岛、核桃来源日程、Gourmand 连续性或 completion proof，因而是**中断的校准证据**，不是完成轮次，也不得跨进程续接。期间 Master Angler 合法锁定 `(O)SeaJelly` 并进行随机重试；锁定 1.6.15 的 `Stats.checkForFishingAchievements` 明确以 `ObjectType == "Fish" && !ExcludeFromFishingCollection` 为分母，透明鱼类集合也包含三种 Jelly，因此不得把 Jelly 从权威分母中删除；本轮约数十次未命中不足以证明执行器缺陷。
- 本轮确认的流程缺陷是验证顺序：姜岛下游尚未做边界存档 smoke，就先投入了 day-zero 长链。已从隔离的 `E:` 测试存档复制只读基线到 `I:\StardewAITraining\golden-walnut-source-schedule-v102\validation-saves\ginger-island-unlocked-baseline-20261103\CrabPotAudit_442159967`；其主存档 SHA-256 为 `7F34A5731208788F37EF2D1F76B64B890466293878F876ABCA6FE4A05A052CB0`，`SaveGameInfo` 为 `0F2716DE726B87A8EC9AAE6B89E1713C6207BD24EA119E8DC188DFDF2C0620FC`。恢复工作后的唯一下一步改为：先在该副本上复用现有产品执行器验证已解锁姜岛的捕鱼地点、Gourmand、核桃来源和原生回执；通过后再启动新的 day-zero 终局证明。不得启动第二套岛屿执行器，也不得改写用户游玩存档。

> **顺位修订 2026-11-03T15:46:34+08:00：** 此条记录的岛屿基线和失败事实保留，但其“唯一下一步”随 Master Angler 暂停。当前下一 primary 已在本文件顶部改为 Rusty Key；岛屿边界 smoke 在第 10 项恢复时先做，之后才允许该项 fresh-save 终局长跑。

## 2026-11-01：v97 暂态移动门误判已修复，v98 启动前需恢复磁盘余量

- v97 `golden-walnut-source-schedule-v97-20261101-182500` 在 Year 1 Spring 10 13:50、iteration `329` 按连续错误门安全停止；保留 `204` 条 verified Product action 和完整滚动证据。它尚未首次登岛、未生成 source schedule，状态仍为 incomplete，且不得冒充新 Gourmand 回执或正式训练证据。
- 唯一终止原因是 Master Angler Teacher 把透明桥的暂态 `future_route_movement_timing_context_not_ready` 当成校准文件漂移并连续抛错。当前代码只把**精确且唯一**的这一暂态原因转换为非训练、零候选的当前轮次暂缓结果，并交还共享日计划；游戏版本、哈希、时钟常量、移动范围等真实校准漂移继续失败关闭。
- 修复后的 GoalConditionedBootstrap 与 LiveTrainingLoop Release 构建均为 `0 warning / 0 error`；聚焦 `self-test-current-stage-one-collection` 通过。对 v97 iteration `329` 原始现场快照的离线重放稳定得到零候选及原始暂态 blocker，不再抛异常。
- 磁盘复核确认现有 rolling retention 只保留**单轮内部**最近 `64` 个迭代，不限制历史 run root 数量。v70-v97 顶层目录当前合计约 `17.49 GiB`，而 Windows 自动管理的 `I:\pagefile.sys` 另占 `16.00 GiB`；10 月 27 日保留的旧 `artifacts` 约 `34.16 GiB`，此后仅改写约 `0.14 GiB`，不是本次重新膨胀来源。未执行任何清理。
- 长跑外层包装器现增加启动余量门：默认在创建 fresh save 前要求可用空间至少为运行中硬停止底线 `8192 MiB` 加新轮次余量 `2048 MiB`，并把各输出卷启动时余量写入成功 summary。该门只拒绝危险启动，不自动删除或覆盖历史证据；PowerShell AST 与包装器 source-guard `9/9` 通过。
- 同一外层包装器现另在 fresh save 之前调用只读跨轮预算器：默认只管理同一父目录下名称以 `golden-walnut-` 开头的 run root，要求新轮次加入后不超过 `24` 个，并要求已有历史根逻辑体积不超过 `12288 MiB`。任一上限或重解析点异常都会失败关闭并报告逐根清单；预算器明确 `cleanup_performed=false`，不会自动删除、归档或覆盖证据。该门补足单轮 `64` 迭代 rolling retention 无法限制历史 run root 数量的缺口。
- 下一入口仍是全新 day-zero v98，并同时启用 `RequireSourceScheduleReady` 与 `RequireGourmandContinuationReady`。显式审核回收后，`I:` 当前约 `26.4 GiB`、`E:` 约 `11.0 GiB` 空闲，均已满足默认 `8192 MiB` 运行中硬底线与 `2048 MiB` 新轮次余量，可以恢复该入口；后续仍由启动门按实时可用量失败关闭。

## 2026-11-01：Master Angler 前置完成证明已接入产品链，v97 继续旧代码校准

- 主线提交 `f588383`（合入为 `6f164814`）建立冻结 episode 的 Gourmand 三段原生连续性回执：同一 run/save/player/game/manifest/evidence root 上只接受 `0 -> 1 -> 2 -> 3` 三次原生请求，并逐项核验 executor receipt、effects、facts、数量、哈希和无直接状态写入。执行回执现由 fresh-save archive 不可变复制，rolling retention 不再删掉其唯一证据。
- 提交 `500df481` 与 `f79212f2` 把该回执接入唯一 Master Angler fresh-save prerequisite audit 和产品校准入口。没有回执时仍精确保留 `ginger_island_access` 这一项缺口；只有 source schedule 已独立验证、回执重建完全一致、三段连续且零 blocker 时，Ginger Island family 才可升为 `implemented_and_bound`，缺口数才从 `1` 降为 `0`。
- 提交 `1621024f` 与 `99b3e5b0` 在该审计上增加唯一完成证明及其 CLI/产品包装器接线。证明由同一进程重建审计后生成，哈希绑定审计、dependency expansion 与 Gourmand receipt，并严格要求 `72` 个不重复鱼种、`14` 个不重复且全部已绑定的前置族、`3` 次 Gourmand 原生转移、零未知地点和零 blocker。真实 CLI 与 PowerShell 包装器集成路径均已覆盖，相关回归 `334/334`、PowerShell AST 与 `git diff --check` 通过。
- 当前真实 catalog/window/dependency 的 game-free 基线重建结果同样为 `72` 个鱼种、`14` 个前置族、未知地点 `0`，且唯一缺口为 `ginger_island_access`；完成证明的严格分母不是夹具常量替代真实输入。
- 提交 `97721637` 将同一函数接入实际长跑收尾：source schedule 独立重放通过后自动生成 audit；同时要求 Gourmand continuation 时再自动生成 completion proof，并把两者路径写入产品与外层工作流 summary。缺少必需 authority、audit 或 proof 会直接失败关闭，不再要求人工执行第二条证明命令。
- 当前主线又接入证明驱动的 dependency closure：完成证明会被独立重建并与源 dependency 路径/SHA-256 复核，随后才派生一份 run-local 闭合图，把唯一 `complete_master_angler` direction 标为 `complete`、清空旧 blocker，并把 proof 路径/SHA-256 写入前置清单节点。源 authority JSON 保持只读；错哈希、伪造 proof、重复 direction 或关键节点缺失均失败关闭。专项 `79/79`、真实 verify-only 包装链 `135/135`、两份 PowerShell AST 与补丁检查均通过。
- 该晋升和完成证明都只是 prerequisite implementation/binding 证据；回执、总审计与完成证明继续强制 `training_label_eligible=false`，不得解释成逐鱼捕获证明、正式 Teacher/native 三分区证据或训练授权。
- 当前隐藏静音 v97 `golden-walnut-source-schedule-v97-20261101-182500` 由上述改动前的二进制启动，只用于保留 source-schedule 校准证据；最新低频观察已越过 iteration `216`、`149` 条 verified action，仍在 Stage One collection 且无 `stage=error`。它无法热加载 Gourmand 连续性、完成证明或闭合图，结束后也不得冒充新回执。
- v97 结束后的唯一下一原生入口是全新 day-zero v98，同时启用 `RequireSourceScheduleReady` 与 `RequireGourmandContinuationReady`，使用新的不可变 execution receipt archive。只有 v98 生成 source schedule、独立重放成功、完成精确三段 Gourmand，并由同次产品收尾自动落盘 `72/14/3` completion proof 与闭合 dependency expansion，才形成缺口 `0` 且可交给 Goal Method / coverage 下游消费的运行时证据；正式训练状态在新的准入门通过前仍为 **false**。

## 2026-10-31：v92 保留证据，#377 已修复，下一入口锁定 v93

- v92 的 13 日 Crop Teacher 控制段已在 iteration `99` 正常完成，形成 `96` 条 verified Product action 和 `12` 条日界回执；iteration `100` 已开始 Stage One collection。它仍是 `--skip-training` 校准，不含 Student 参数、checkpoint、GPU 训练或正式策略数据，也尚未生成 Golden Walnut source schedule。
- #377 的三个当前主线错误已由 PR `#378` 在 merge commit `21d43710` 修复：独立 verifier 包装器改读 `state.identity` 并具备不可变产物的包装器级正负向测试；prepared-soil 旧兼容不再穿过静态阻挡；Master Angler 延迟出发重新核算全路线与终端预留。聚焦 `127/127`、GameFreeGovernance `609/609`、Backend `257/257`、bootstrap hermetic 与远端质量门均通过。
- **唯一 WIP=1 不变**：不得续接 v92；从 `21d43710` 启动全新 day-zero v93，继续首次真实抵达、核桃来源增量、First Parrot、Turtle、House、source schedule 生成与冻结 manifest 独立重放。全程保持 `--skip-training`、`formal_product_training=false` 与当前 `5/19` CoverageGateReady。

## 2026-10-30 v90 姜岛先决条件循环已在代码层拆除

- 对等 `gpt-6.1-sol high` 审查用 fresh-mainland 反例证明：锁定姜岛会阻止终端路线，终端路线缺失又阻止 Master Angler intent，现有姜岛修船/核桃/鹦鹉链因此无法被调用。
- 普通捕鱼仍必须取得完整终端路线。新通道只在精确权威岛屿鱼窗口成立且透明 access 明确锁定时生成一条 control-only prerequisite intent；它不伪造路线证据，复用唯一候选评估、DailyPlan、ActionQueue 与 Product Executor。
- 控制候选在 Teacher 偏好前按稳定顺序编译，learner 信号归零，明确 `teacher_preference_label_eligible=false`、`formal_training_authorized=false`，策略轨迹写入端再次按 marker 拒绝。解锁后必须 fresh 重建普通完整路线。
- v90 是旧代码启动的 `--skip-training` 校准运行，只保留为修复前证据。合入后从全新 day-zero v91 重跑既定出口：首次抵达、来源增量、First Parrot、Western Turtle、Island House、schedule 生成和独立重放。

## 2026-10-30 v88 已冻结，v89 必须使用严格抵达与玩家身份

- v88 在 iteration `250` 检查点停止；最后完整 append 为 iteration `247`、累计 `215` 条 verified action。它完成两轮 Crop Teacher 并继续 Stage One collection 控制链，但尚未首次登岛，因此不满足 source-schedule 退出条件，也不计正式训练。
- 下一轮开始前已关闭两项证据正确性缺口：source schedule 的每个 before/after snapshot 必须绑定 day-zero 初始玩家；`journey.island_reached` 不再读取启航即递增的 `boatRidesToIsland`，只读取真实 Island 入场产生的 `Visited_Island` pending/received 状态或当前 Island 地图。不得把 `BoatJourney`、启航事件或统计计数单独当作抵达。
- 当前集成验证为身份行为回归 `8/8`、CI guards `2/2`、GameFreeGovernance `532/532`，TransparentBridge/LiveTrainingLoop Release 零警告零错误。下一 WIP=1 是从全新 day-zero v89 重跑相同七步出口并冻结真实 manifest；成功后必须用现有 game-free Bootstrap builder 独立重建和字段比较 schedule，再进入 #345 Provider/Student 门。

## 2026-10-30 v87 原生回归已证明补水走廊修复

- PR `#369` 已合入 `main`，本机默认 MSBuild `GamePath` 由被忽略的机器配置固定到 E 盘隔离 SMAPI 运行时。普通构建不再进入 ModBuildConfig 对失效 F 盘路径的自动发现；具体绝对路径不进入公开仓库。
- v87 从全新 day-zero 运行到 iteration `104` 后在完整日界安全停止，取得 `100` 条 verified action，stderr 全为零。第二轮 Teacher 的实时承诺绑定 `(72,27)`，补水准备格为空；iteration `93` 完成场地准备，`100` 完成补水入口，`102..104` 完成回家、睡眠和新日。v86 树桩阻塞没有复现。
- 本轮仍是 `--skip-training` 校准，不能增加正式训练分子。下一 WIP=1 入口仍是 Golden Walnut source schedule：新运行身份继续首次登岛、核桃来源增量、First Parrot/Turtle/House、source schedule 生成及独立重放；不得因为走廊回归通过而跳过 #345 Provider/Student gate。

## 2026-10-30 v86 已耕补水走廊不得被重复清障替换

- `golden-walnut-source-schedule-v86-20261030-151929-exclusive-recovery` 从新 day-zero 身份在 iteration `97` 前取得 `94` 条 verified Product action、`9` 条日界回执并完成第一轮 Crop Teacher。第二次作物 handoff 在 iteration `81` 正确形成 `8` 格防风草承诺，但 iteration `98..102` 重复要求清理同一未升级斧头无法处理的 `2x2` 树桩，故安全停止。运行仍为 `--skip-training`。
- 根因位于透明布局语义而非执行器。旧 `watering_refill_approach_standable` 直接使用静态 occupancy，`HoeDirt` 即使无作物也被视为阻挡，导致首轮已清走廊不能复用。透明桥现新增 `native_walking_collision_blocked`，以原生 `IsTileBlockedBy` 的 passable 忽略掩码计算；Teacher 只对旧证据中的 `already_prepared` 做兼容，其余未知或不可执行阻挡不放行。
- v86 原始 request 离线重放得到 `(70,27)` 补水入口和三个普通对象清理格，不再包含 `(74,25)/(74,26)` 的树桩格。Teacher hermetic、GameFreeGovernance `530/530`、Backend `257/257` 与 TransparentBridge Release 均通过。代码变化后必须从全新 v87 重跑；不得把 v86 尾部、离线重放或 fixture 当作 source schedule。
- 构建环境同时完成一次性修正：仓库支持被忽略的 `Directory.Build.local.props`，本机文件与用户级 `GamePath` 均固定到 `E:\StardewValleyAICompanion-runtime\Stardew Valley`。公开仓库不提交本机绝对路径，普通构建也不再反复命中失效的 F 盘 Steam 目录。

## 2026-10-30 v85 独占恢复不得被战略 handoff 抢占

- `golden-walnut-source-schedule-v85-20261030-120056-legacy-dialogue` 从全新 day-zero 身份推进 `425` 次迭代，取得 `306` 条有语义 verified Product action，完成三轮 Crop Teacher，并推进到 Spring 22 / `total_days=21`。第三轮收获为 `8/8`；邮件、任务 `13`、钓鱼、容量恢复、出货与多次日界也均走现有原生执行链。该运行继续 `--skip-training`，没有参数训练或正式 checkpoint。
- iteration `421` 的日计划已选定 Backwoods -> Farm 的 `recovery:return_home`，ranking 同时明确 `exclusive_recovery_override=true`。旧主循环却先读取遗留的 `crop_production_teacher` planning handoff 并立即 dispatch，导致 19:00 的作物 Teacher 在不存在当前季可成熟种子的现场连续失败。不得通过放宽种子、季节或经济约束修复。
- 唯一修复点在 `PlanningHandoffCapture` 的调度资格：完整 handoff 遇到独占恢复时返回 defer，主循环记录原因后让原 queue 继续执行；恢复完成后按现有 fresh-replan 机制重新求解。没有第二套恢复、作物规划、编译或执行器。
- v85 原始 `ranking-response-0421.json` 对新程序集的离线结果为 `DeferHandoff=true / DispatchHandoff=false`。专项 `15/15`、GameFreeGovernance `530/530`、Backend `257/257`、LiveTrainingLoop Release 零警告零错误。未过滤旧 Core 套件仍有 36 个与本改动无关的 current-main 基线失败，另有 3043 项通过；它们不被伪报为本切片全绿，也不改变当前 WIP=1。下一固定入口为全新 day-zero v86，并继续既定 Golden Walnut source-schedule 退出条件。

## 2026-10-30 v84 旧式日界对话修复与 v85 入口

- `golden-walnut-source-schedule-v84-20261030-101752-menu-preflight` 从全新 day-zero 身份运行并继续携带 `--skip-training`。它证明 PR #365 的菜单预检能跨过 v83 边界：Crop Teacher 完成，两轮作物阶段取得 `5 + 4` 条日界回执和第二轮 `8` 条收获回执；Stage One 继续执行邮件、任务 `13`、钓鱼、容量恢复、出货和路线。iteration `198` 前共有 `147` 条有语义 verified Product action。
- iteration `198` 的睡眠把日期原生推进至 Spring 11 `06:00`，随后活动 `DialogueBox` 已关闭，但 `Game1.currentObjectDialogue` 仍有旧式全局对象对话，状态为 `dialogueUp=true / active_menu=none / CanMove=false`。旧透明 movement context 漏读 `dialogueUp`，旧睡眠执行器也只处理活动菜单，于是返回 `post_sleep_world_not_stable`。锁定 1.6.15 反编译确认：`DialogueBox.closeDialogue()` 可在队列尚有内容时保持 `dialogueUp=true`；原生 `Game1.pressActionButton()` 负责逐条推进并最终恢复移动。
- 修复没有增加第二套睡眠或对话系统。现有睡眠执行器仅在日界精确前进一日、菜单为空、无问题/事件/角色对话且 `currentObjectDialogue.Count > 0` 时发送有界原生右键脉冲；完成、阻塞或超时时都释放 override，未知状态保持失败关闭。`NativeNewDayWorldStable`、透明 native-control gate 和 `NativeControlSettleRecovery` 均显式要求 `dialogue_up=false`。
- iteration `199..227` 的 `29` 次 recovery wait 只有 wait 计数变化，时钟事实保持 `600->600`。no-progress 现把附带的未变化事实视为等待的一部分，只有其他真实游戏事实变化才重置 streak；达到 `MaxQueueNoProgressStreak` 后停止。旧报表中的 `176` 必须拆为 `147` 条有效执行和 `29` 条隔离校准噪声，不得写入训练或 coverage。
- v84 不能跨代码变更续接。下一固定入口是全新 day-zero v85，顺序仍为首次登岛、Golden Walnut 来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放，再进入 #345 准入门。没有 Student 更新、checkpoint、GPU 训练或正式训练授权。

## 2026-10-30 v83 长跑、失败边界与 v84 入口

- `golden-walnut-source-schedule-v83-20261030-074742-global-settle` 从全新 day-zero 身份运行 `324` 次迭代，取得 `223` 条 verified Product action、`433` 个 fresh-save episode、`8` 条收获回执与 `5` 条日界回执。Crop Teacher 于 iteration `140` 完成；之后 Stage One 继续完成邮件、任务 `13`、钓鱼、背包恢复、Fishing `ItemGrabMenu` 恢复、出货、日终恢复和 Spring 17 -> 18 原生睡眠结算，金钱 `20 -> 461`。滚动保留窗口正常维持 `64` 次迭代并回收失去引用的内容寻址块。
- 最终失败快照固定在 Spring 18 `16:30`、`ScienceHouse (8,20)`、Carpenter `DialogueBox`。路线 timing 未就绪但菜单本身已有安全 `recovery_close_menu` 候选；Stage One 却在选择该候选前调用 Master Angler 目标日期意图构建器，连续五次抛出 `future_route_movement_timing_context_outside_calibration_scope`。根因是控制编排顺序，不是路线校准范围不足。
- PR #365 已将 mandatory menu 预检拆为独立 partial 控制链并放在严格意图构建之前。预检只在活动菜单时查询 recovery/profession/mail 三项，保留职业与信件处理对通用关菜单的抑制；恢复继续使用现有候选评估、排名、日计划、动作队列与 Product Executor，控制工件不生成 Teacher 标签。无合法恢复时仍走原严格构建器，不吞掉未知菜单错误。
- 验证为专项 `7/7`、Core `529/529`、Backend `257/257`，E 盘隔离 SMAPI `GamePath` 下完整解决方案测试/构建通过。v83 显式 `--skip-training`，没有 Student 参数、checkpoint 或 GPU 训练。其失败尾部不能跨未保存边界续接；下一固定入口是全新 day-zero v84，继续首次登岛、Golden Walnut 来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放。

## 2026-10-27 落盘流程有界化

- 运行快照默认从普通全量 JSON/停止式保留改为内容寻址 gzip/64 轮 rolling；显式 `plain` 或 `stop` 仍可用于兼容诊断，但不再是普通入口默认值。
- 逻辑证据不合并：before、每个执行项 after、队列终态 after 和执行回执仍可逐项审计。物理层按 SHA-256 共享不变字段和重复终态，所以“逻辑不缺、引用不漏、内容不重复”。
- rolling 已覆盖实际写出的 dispatch recovery、native save boundary 与 Crop Teacher 文件族。删除过期逻辑文件后重新扫描所有剩余内容寻址清单；只有受管 `_snapshot-blobs/<2hex>/<sha256>.json.gz` 且无任何引用的文件可被回收。任何无效 JSON、畸形清单或缺失引用都会停止块回收并保留现场。
- `artifact-retention-state.json` 以原子替换记录每轮保留与物理回收统计；LiveTrainingLoop 最终报告记录实际 artifact mode、retention mode 与窗口。聚焦回归为 Core `29/29`、Backend `5/5`，工具独立构建零警告零错误；没有启动游戏或生成新的长跑目录。

## 已收口增量 · Golden Walnut v26 收获容量恢复

- v26 `golden-walnut-source-schedule-v26-20261026-023455` 已从全新隔离存档真实推进到 Spring 17（`total_days=16`）：购买 `14` 份花椰菜种子，完成首轮整地/播种/浇水/收获，处理邮件与任务 `13`，取得竹鱼竿并钓到 `(O)129`，再按节日门禁等待后由 Beach 原生返场，开始第二轮防风草生产。第 `173` 次迭代前旧路线、节日和作物闭环均未回退。
- 第 `173` 次真实快照为 Farm、背包 `12/12`、8 格成熟作物；作物批次正确生成 8 项 `executor.harvest_crop`，但普通手持收获无空槽，编译器连续五次失败关闭。跨日后最近钓获字段已经清空，证明 v25 的 exact-catch-only 恢复不足以覆盖 Crop Teacher 持权阶段。
- `economy.ship_items` 候选早已携带 `available_stack`，实际遗漏发生在 `EventCandidate -> PolicyEventCandidatePrediction -> Grandpa clone` 中间契约。该字段现已贯通，并有排名回归防止再次丢失；没有新增候选池或第二套出货实现。
- Crop Teacher 在编译收获批次前检测 fresh 满包状态，先把控制权交给统一容量恢复模式。恢复模式跳过 Master Angler 意图构建，不依赖 `RequireGoldenWalnutSourceScheduleReady`，也不调用策略模型；它只通过 Backend availability、确定性候选物化、`DailyPlanCompiler`、`ActionQueueCompiler`、共享路线和原生运输箱执行器。恢复队列不能结算被中断的收获步骤，完成后必须 fresh 重规划原批次。
- 安全选择只允许上游已判 available、`quantity=1`、`available_stack=1` 的候选；优先 Full Shipment 首次贡献，否则仅接受 `<=50g` 的低价值单件。既有自动出售保护继续排除种子和其他预留资源。learner score/model source 被中和，`formal_training_authorized=false`。
- v26 真实失败快照离线重建出 5 个合法出货候选，确定性选择 `(O)92 x1 / 2g / Full Shipment`；正式编译为 `pending / 2 items / first=move_to_tile / all pending`。未选择 6 个花椰菜、5 个木材或受保护种子。显式续跑会重建并结构比对原计划，源账本保持只读，当前 run 使用派生账本；只有已有后续日界回执的收获才作为已落盘进度继承。定向续跑/账本测试 `16/16`，此前定向测试 `42/42`、相关矩阵 `137/137`、Release solution `0 warning / 0 error`。
- 隐藏静音原生续跑 `crop-teacher-v26-resume-fork-20261026-040111` 已通过：`8 crops / 0 empty / sap x1` -> 原生投递 `(O)92 x1` -> `8 crops / 1 empty / sap x0` -> 同一 commitment 的 8 格收获全部 verified -> `0 crops / 0 empty / parsnip x8`。派生账本为 `8 harvest + 4 day boundary`，源账本保持 `0 + 4`，crop run complete。下一固定动作恢复为 collection Teacher、首次登岛和 Golden Walnut source schedule；该短验收没有新日界存档，不能冒充 source schedule，全局准入数字与 `formal_product_training=false` 不变。

## 当前增量 · Crop Teacher 到 collection Teacher 的原生交接

- 新的显式 continuation 模式只允许从已验证的 Crop Teacher 只读源账本进入，并同时校验 collection requirement inventory、acquisition lowering、Master Angler window index 与 route timing calibration。旧的首日完成快捷退出仅在未请求 continuation 时保留。
- v27c 已真实完成 Crop Teacher 并进入 Master Angler 路线，但首个路线候选失效后的权威同分状态错误落入通用 daily-plan fallback，后者重新派发了 Crop Teacher。该运行已立即停止，未把后续错误路线冒充 collection 证据。
- 现将“可用于 Teacher 标签的唯一权威偏好”与“控制器可从同样正确的选项中任选其一推进环境”分离。同分时 Teacher preference 仍保持 blocked/null；控制队列使用稳定顺序、归零 learner 信号并单独落盘 `stage_one_collection_control_selection.v1`，固定声明 `teacher_preference_label_eligible=false` 和 `formal_training_authorized=false`。除同分外的 authority blocker 会终止有界运行，不再落入作物 handoff。
- v27d 隐藏静音回归已再次完成作物恢复与 8 格收获，随后执行 Master Angler Teacher 路线；fresh 失效后成功生成一条 control-only Backwoods 路线，并连续取得 3 条 verified native action。全过程未再次派发 Crop Teacher，也没有产生伪 Teacher 标签。短测为 `9/10`，唯一未达到 10 的原因是 `12` 次尝试预算包含两次候选失效/replan；因此结果保留为受限 incomplete，而非伪装成功。
- v28 将同一链推进到 `18` 条 verified native action，普通 Teacher、control-only 路线、fresh 失效和重绑均真实运行且没有 Crop Teacher 回退。新的停止点是严格 selection contract 拒绝 Construction Bundle 的两个 `99` 木材槽同时认领同一条木匠店连接候选；这不是 Agent 汇报错误，而是会制造伪 Teacher 信用的真实数据建模缺陷。
- 修复统一为一次执行在同一 selection group 最多取得一个信用，跨独立目标的精确信用仍保留；route connector 只形成 `authoritative_collection_rolling_route_step`，不再冒充 acquisition endpoint；实际 acquisition endpoint 的收据只要求本次候选数量，而不是整个长期需求数量。v28 第 `25` 次真实工件已离线重放通过：木材候选只绑定 Crafts Room alternative `0`，其 Full Shipment 与 Community Center 信用均为路线进度，Teacher 继续因四个权威同分候选而阻塞且不输出标签/队列。current collection 全链自测通过，全解决方案 Release 为 `0 warning / 0 error`。
- v29b 把 Spring 7 基线存档误配给 Spring 13--17 账本，严格校验在执行前拒绝；该失败没有动作或训练数据。恢复循环现于首张真实快照执行一次性兼容性门，不再把同一错配重复记作五次运行错误，Planner 原有失败关闭不变。
- 正确使用 v26 Spring 17 隔离存档后，v29c `golden-walnut-collection-v29c-20261026-053000` 已通过：`31` 次迭代取得 `24/24` verified action、`24` 行执行数据、`2` 次 Teacher 队列和 `5` 次 control-only 队列；第 `25` 次和第 `26` 次均成功，旧 malformed group 没有复现，日志零 error。Crop Teacher 派生账本保持 `8 harvest + 4 day boundary`，源账本只读。
- day-zero v30 在 `14` 条 verified collection route action 后得到精确的 `blocked_no_current_collection_candidate / unified_current_collection_candidate_membership_is_empty`，旧循环因无条件终止没有进入共享日计划。新增 `StageOneCollectionFallbackPolicy` 只允许 `RequireGoldenWalnutSourceScheduleReady=true` 与该精确状态/原因组合让权；普通 collection continuation 和任何其他 authority blocker 保持失败关闭。
- 隐藏静音新档 v30b 已原生验收让权：第 `16` 次记录 `stage_one_collection_teacher_yielded`，随后共享日计划完成第 `15` 个 verified action 并产生 `1` 条策略轨迹；第 `17` 次派发既有 Crop Teacher 的 `84` 项原生计划，第 `18` 次继续 verified。诊断达到该退出条件后人工停止，三端及端口已回收；它不是 source schedule。定向测试 `6/6`，隔离 Release solution `0 warning / 0 error`。
- v31 已从新的 day-zero 隔离存档取得 `179` 条 verified action，真实完成两轮 Crop Teacher、节日等待、满包收获恢复和一次 `(O)131` 捕获；人工停止前零 `stage=error`。停止点为 Spring 17 09:10、背包 `12/12` 时 `(O)145` 在 Forest/Town 间往返：透明鱼规则完整，本地抛竿因满包正确阻塞，但跨地图路线遗漏了同一终端容量门。
- Master Angler 跨地图路线现先检查透明背包容量，满包时由既有 Stage One 安全单件出货恢复接管。v31 原第 `194` 次排名/快照离线重放选中合法出货；v31c 原生续跑从只读源存档的哈希一致副本验证 `12/12 -> (O)390 原生移除 -> 11/12 -> Master Angler 重新规划`，不再换图振荡。v31c 因误开 day-zero-only 证据归档门而在已执行动作的归档阶段停止，不能冒充完整 source schedule。定向测试 `7/7`，隔离 Release solution `0 warning / 0 error`。
- v32 `golden-walnut-source-schedule-v32-20261026-231505` 从新档取得 `75` 条 verified action 和 `7` 条日界后，真实暴露补水合同断链：Teacher 已证明并准备通道，但 run plan 没有保存可触发原生补水的终端接近格，水量归零后 `executor.water_crop` 只能失败为 `no_reachable_water_source`。
- 修复将 `watering_refill_approach_tile` 贯穿 Teacher decision/receipt/run plan，并从种植格中保留；控制层按实时 `water_left` 只编译当前水量可覆盖的浇水前缀，归零后把保存格交给既有 `move_to_tile + BFS + route repair`，抵达后仍由既有 `executor.water_crop` 原生补水并完成剩余地块。没有新增平行移动、清障或浇水系统；resume 包装器也恢复了原生 save-boundary 参数。
- v33 首轮新档到 `53` 条 verified action 后主动停在 `6` 格待浇、`4` 水边界，以便加入部分批次约束。续跑 `crop-teacher-refill-v33b-20261027-0023` 已依次验证 `4 次浇水 -> move_to_tile Farm(72,27) -> 2 次浇水`，最终跨到 Spring 13，得到 `45` 条 verified action、`12` 条日界、`6` 条收获回执、`0` 条错误并令 `crop_teacher_run_completed=true`。本轮没有策略标签，不能冒充正式训练或 Golden Walnut source schedule。
- 下一固定动作是从新的 day-zero 隔离存档继续完整长跑，取得首次登岛、至少一个 Golden Walnut 来源增量和 First Parrot/Turtle/House 三项原生跃迁并导出 source schedule；v31/v31c 均未启动正式训练。

## 已收口增量 · Golden Walnut v24 清障成本修复

- v22 已把节日等待后的角色送回 Farm 并生成场地接近队列，但长距离 route repair 没有读取全地图 blocker 身份；第一轮修复接通现有 `locations.route_blockers` 后，v22 原失败输入可以重编译为 pending。该结果只证明旧输入已可编译。
- 全新 v23 `golden-walnut-source-schedule-v23-20261025-233534` 已真实取得 `137` 个 verified native action、`12` 条原生日界和 `6` 条逐格收获回执。首轮作物 run 的接近、整地、播种、浇水、收获与结算全部完成；随后完成竹鱼竿邮件/任务路线、两次钓鱼、节日等待和 Beach -> Town -> BusStop -> Farm 返场。
- 第二轮作物接近在第 145--149 次迭代暴露成本盲区：同为三次清障时，旧贪心选择近处的 `(73,23)` 成熟枫树而不是 `(73,24)` 杂草。透明字段给出该树需要 starting axe `15` 次挥砍、`30` 点体力；旧 route repair 同时写死 `max_tool_swings=8`，故编译阻塞并按连续错误预算停止。
- 当前修复仍只使用一套 `move_to_tile + BFS + executor.clear_obstacle`。选择器在 `max_route_repair_clears` 内按每格透明清障分钟和体力执行有界最小成本搜索，编译项携带每个障碍的精确挥砍上限、分钟与能量；全地图树若没有精确成本则失败关闭。v23 原输入已由 Backend 严格重放为 `pending / 7 items / 0 blocked`，第三个清障从成熟树变为杂草，最终接近点不变。
- 验证：新增回归后 `ActionQueueCompilerTests 177/177`；Core 全量 `2959 passed / 21` 个既有基线失败；隔离 Release solution 构建 `0 warning / 0 error`。v23 仍为 incomplete，不能冒充修复后的原生清障证据。
- 全新 v24 `golden-walnut-source-schedule-v24-20261026-003430` 已关闭原生退出条件。它从 day-zero 新档运行到第二轮作物 handoff，在有界停止前累计 `145` 个 verified action、`12` 条日界和 `6` 条收获回执。第 144 次队列选择并原生完成两格杂草 `(50,27)`、`(71,27)`，而非成熟树；整条 `move/clear/move/clear/move` 队列为 `5/5 applied + primitive verified + fresh`。第 145--150 次继续完成 `till/move/till/move/clear/till`，每项均 `1/1 applied + verified`，旧编译错误未复现。
- v24 在上述有界验收点人工停止，不是 source schedule 或正式训练。对其中的 `trajectory_rejected:InvalidOperationException` 逐项重放后确认：`recovery.stabilize_day` 属于校准专用控制动作，`quest.advance` 属于证据专用聚合动作，二者按能力注册表都不进入策略训练；因此不存在应补写的睡眠/任务 Teacher trajectory。LiveTrainingLoop 已在 builder 前复用统一准入过滤器，将两类成功执行记为 `policy_training_option_not_admitted`，继续保留执行证据且不污染策略数据集。真实 v24 第 `88`、`93`、`94`、`95` 次制品离线调用全部得到 `0 appended / 1 skipped` 和该精确原因码，聚焦回归 `11/11`。下一动作恢复 Golden Walnut 首次登岛主线。

## 当前增量 · Golden Walnut 长跑相邻访问清障

- `golden-walnut-source-schedule-v17-20261025-203000` 在第 47 次迭代真实失败于 `till_soil_no_adjacent_route_stand_tile`。两个 planting tile 本身可耕，但周围可移除障碍让执行器没有合法相邻站位；旧 planner 只清目标格，因而正确失败关闭却无法推进。
- 场地准备现复用同一候选与执行器，在阻塞原因**仅为**无相邻路线站位时，确定性选择相邻 `executor.clear_obstacle` 或 `executor.break_farm_resource_clump`，状态标记为 `clear_access_then_fresh_snapshot_rebind`。执行后仍须 fresh 重绑；缺工具、体力不足、超时或其他阻塞不能借此放行。专用 snapshot bounds 在仍不超过 `32x32` 时只扩一格，以覆盖外围站位和障碍。
- v17 原第 47 次真实请求已用当前源码重建为 ready：`blocked_count=0`、`clear_then_rebind_count=2`、`access_clear_then_rebind_count=2`，精确选择 `(70,19)` 石头和 `(71,21)` 杂草。该证据证明编译修复覆盖原失败输入，但不是一次新的原生执行。
- 全新修正版长跑 `golden-walnut-source-schedule-v17b-20261025-181200` 的随机布局走直接耕地分支，最终真实完成六格整地、播种、逐日浇水、六格收获和作物 run 结算，形成 `94` 个 verified episode、`12` 个日界与 `6` 条收获回执。随后统一 Stage 1 Teacher 完成 `spring_2_1` 邮件及任务 `13` 的三段 Beach 路线，确认作物 Teacher 能正常退场。
- v17b 的第 100 次迭代在事件 `739330` 处失败：候选本身为 `story.advance_event / ready_now`，却被 planning-handoff 空绑定过滤为 `empty_action_list`。队列构建现对原生剧情 interrupt 使用独立的精确过滤，只接纳 `story.advance_event`，不应用战略 goal binding；普通 handoff 仍失败关闭。全解决方案构建 `0 warning / 0 error`、聚焦回归 `64/64`。v17b 保留为 incomplete，不得续接其未保存尾部。当前下一退出条件不变：从新隔离存档重跑，直到首次登岛、至少一个 Golden Walnut 来源增量和 First Parrot/Turtle/House 三项原生跃迁齐备，再由既有 builder 导出 source schedule。
- v18 全新隔离长跑再次结算作物 run，并真实通过事件 `739330`：原生剧情回执为 `applied/verified`，透明库存出现竹鱼竿，随后执行两次 Beach 钓鱼候选。该 run 在新作物 handoff 的 Beach→Farm 首段暴露附带系统提示关闭缺口：连接器走到 `(38,0)` 后，无角色普通 `DialogueBox` 仍在逐字显示，旧移动器过快耗尽 `16` 次点击，下一轮 route 因活动菜单而失败关闭。
- 修复复用同一 `executor.close_menu` 与现有连接器。移动器改为按 `dialogueFinished/showTyping/transitioning` 状态变化节流，最多 `60` 次输入、`600` tick；仅当透明字段同时证明非事件、非问题、零响应、非睡眠且无角色/空 speaker 时，Teacher 才先编译 `safe_dialogue_recovery`，并禁止该中断结算原作物步骤。v18 原始失败快照已重放为单项 pending close-menu 队列，聚焦 `17/17` 与全构建 `0 warning / 0 error` 通过。v18 仍为 incomplete；下一固定动作是全新 v19 实跑，取得关闭提示并继续跨图的原生回执后，再继续原 Golden Walnut 退出条件。
- v19 已取得两条真实钓鱼库存增量：两个 `fishing.catch_fish` 回执分别新增 `(O)131`，第二条使背包达到 `12/12`；不能再把该段归类为 collection Teacher 假成功。菜单恢复也确实关闭了系统提示，但 Spring 13 07:10 的 Beach→Town 普通 warp 每次都会重新触发同一原生提示并把玩家恢复到 `(38,0)`。
- 反编译确认这是 `Game1.warpFarmer` 的 active-festival 分支：`weatherIcon == 1`、目标为 `whereIsTodaysFest`，节日前拒绝并弹提示，节日时段进入事件而非普通通行。现由唯一 `ActiveFestivalTransitReader` 同时服务图腾透明读、普通 warp 门禁和未来路线 Teacher；普通路线在节日结束前不可作为普通 transit，当前执行器会在派发前失败关闭，Future Route Teacher 则按 `end + 10 minutes` 重新计算同日最早抵达。没有增加第二套路由器或日期/地点特判。
- `DialogueBox.getCurrentString()` 已按反编译的只读方法体加入 `menu_specific_state.dialogue.current_text`，用于区分重复系统提示；该读取不点击、不推进、不关闭菜单。透明桥构建通过，精确回归 `5/5`。下一固定动作是全新 v20 有界运行，取得“上游避开/延后节日普通 warp”及随后正常跨图的原生回执；通过后继续首次登岛与 Golden Walnut source schedule，不在此处扩张动作集合。
- v20 `golden-walnut-source-schedule-v20-20261025-204818` 已从全新 Spring 1 隔离存档真实走到 Spring 13：作物 Teacher 完成 `12` 条原生日界、`6` 格收获和零重派发结算，随后完成邮件、任务 `13` 的三段路线并执行一条 Beach 钓鱼回执。旧提示循环没有复现；新增普通 warp 节日门禁在作物 Teacher 回退返场时正确阻止 Beach→Town。新的精确失败是跨地图 `move_to_tile` 在“路径存在但当前节日暂时关闭”时没有生成等待，而是保留普通移动；Teacher 路由适配只接受 connector，于第 102–106 次迭代连续报 `crop_teacher_route_compile_invalid` 并停止。v20 固定为 incomplete，不得续接为 Golden Walnut 耐久证据。
- 跨地图编译器现只对可证明同日恢复的 `ordinary_warp_active_festival` 生成有界 `executor.wait_ticks`：每轮最多 `600` tick，保留原目标、开放时间、首段 connector 身份和 fresh-replan 要求；显式 connector 请求在关闭时仍 blocked，永久/未知门禁不变。作物 Teacher 路由校验只接受原 `executor.traverse_connector` 或带精确 festival route-wait reason 的这一等待项。聚焦回归 `203/203`、全解决方案构建 `0 warning / 0 error`。下一固定动作是从 Spring 1 新建完整 v21 耐久运行，验收等待后正常跨图，再继续首次登岛和 Golden Walnut source schedule。
- v21 `golden-walnut-source-schedule-v21-20261025-213726` 已真实完成该验收：`140` 个 verified action、`12` 条日界、`6` 条收获回执；第 100–142 次在 Beach 按 exact 14:10 门禁有界等待，第 143–145 次自动切换为 Beach -> Town -> BusStop -> Farm 三段原生 connector。旧编译错误和提示循环均未复现。
- 抵达 Farm 后出现的是独立的场地接近缺口，不是节日路线回退。角色在南入口 `(40,64)`，新布局在 `(69..73,16..25)`；旧场地准备在尚未接近布局时直接计算局部动作可达性，连续 5 次以 `till_soil_no_adjacent_route_stand_tile` / `clear_obstacle_no_adjacent_route_stand_tile` 失败关闭。v21 因该真实间隙固定为 incomplete。
- 唯一 Teacher 链现增加“接近后准备”的前置编译阶段：从同一 bounded Farm 投影选择原生可站立的相邻格，优先避开所有种植与补水准备目标，再交给既有 `move_to_tile + BFS route repair`，最多 4 个既有清障并 fresh replan；没有作物专用寻路器。v21 第 146 次失败快照确定性重建选中 `(69,26)`，原 state hash 由后端严格接纳并编译为 pending `executor.move_to_tile Farm(69,26)`。下一固定动作是全新 v22：取得原生接近、继续场地准备和无间隙 manifest 后，才继续首次登岛与 Golden Walnut source schedule。

## 当前增量 · 新档开局礼盒透明执行链

- 真实 Spring 1 旧快照只有商店、社交和小游戏候选，缺少领取农舍开局礼盒这一必经前置，导致 Grandpa 最大分数目标只能在低价值候选中选择。现已新增唯一高层项 `rewards.claim_starter_gift`，并贯通透明读取、候选生成、日计划、fresh 编译重绑和 Product Executor 共用的原生运行状态机。
- 透明桥只在当前玩家拥有的 `FarmHouse` 中发布唯一 `giftbox + starterGift` Chest，绑定精确 tile/runtime/item 身份、实时 contents、背包容量、相邻站位和农场类型对应任务（普通农场 `6`、Meadowlands `132`）。运行时只调用原生 `GameLocation.checkAction`，由 `Chest.dumpContents` 完成任务、背包/举起物品和空礼盒移除；禁止直接改 Chest、背包或任务。
- 该项已进入 `earn_money` 的 fresh-save 支撑转移。隐藏静音原生运行 `runtime-starter-gift-20261024-172156` 在真实隔离新档中通过：领取前后 full snapshot 哈希分别为 `e31c45f8cb50c9a9576cab27a5769faf6f193bfffdc80e3a2465c29d8369204f` 与 `eed1329a1bee2d239da5e35961ede172e865f3e0b7b99987dc087e1df608bb71`，405 tick 后 `(O)472 x15`、任务 `6` 和空礼盒移除均由原生回执验证。`EVD-342` 已将本项提升为 five-gate / training-eligible；目录预期为 `241 registered / 162 five-gate / 67 training allowlist`，以本轮生成报告为最终事实源。
- 首次原生运行同时暴露新档淡入时控制门未开放和失败回执写死后状态两处硬伤。执行器现在在 paused/global-fade/freeze/warp 门关闭时只等待且不累计 stuck，故事事件会显式失败关闭；无论成功或失败，回执均从当前 Chest 与任务读取真实 after 状态。
- 领取后真实快照已重放。通用售卖/出货现在对 `player.seed_inventory` 的精确槽位与 qualified ID 双重保护可种种子；透明桥的新快照也直接发布同一保护原因。真实旧快照回放中，种子售卖/出货可用候选为 `0`。
- 新地块布局不属于 `farm.maintain_crops` 的机械职责。Grandpa 解析器现于“赚钱方向优先、已有可种种子、农场无作物、没有精确 ready 赚钱候选”时发出 `planning_handoff_required`，携带 `crop_production_teacher`、`farm_layout` 和 `establish_initial_crop_production`；LiveTrainingLoop 在该状态下禁止把完整候选池交给日计划，因此不会再回退到社交。按需布局采集、自动 Teacher 调度和首日 continuation 均已接通，直接复用现有 decision、ledger、obligation plan、native run plan builder 与动作编译执行链。真实隐藏静音运行 `runtime-crop-teacher-first-day-20261024-183952` 已通过：真实 Bridge 的 `farm_site_preparation_context.v1` 嵌套 `bounds` 契约被用于 80×65 Farm 的 9 分块采集；随后完成 6 单位购种、场地准备、6 格播种、6 格浇水、回家和原生睡眠。49 次控制尝试产生 47 次已验证执行，跨日与存档哈希变化均由既有 durable save boundary 验证。现已把该 continuation 泛化为唯一全日期 `CropTeacherRunPlanner`，并把 `executor.harvest_crop` 接回同一机械批次编译链；持久化 transition ledger 逐地块记录已验证原生收获，部分成功可恢复、重复执行被抑制。每个非终端 run day 还必须持有独立的日期加一和存档 SHA-256 变化回执，回执写入前循环只等待落盘，不执行下一天。次日仅保留 5 株、计划日未成熟、错误种子和无回执缺株现在只对明确的战略漂移触发最多 8 次自动 Teacher 重派发；重派发复用同一 capture/dispatch helper，旧 plan/ledger 保留作 lineage，存档边界缺证不会被误分类。完整周期 `runtime-crop-teacher-full-cycle-ledger-fix-20261024-211500` 已修正并验证水量守恒：无补水路线时 Teacher 只选择水壶剩余量可覆盖的 `3` 格花椰菜（`36/40` 次），从 Spring 1 连续运行到 Spring 13，写入 `12` 条存档边界和 `3` 条逐格原生收获回执；收获批次绑定来自实际 `effective_queue_item`，账本重放幂等，运行零重派发完成。作物生产交接退出条件已达到，不新增第二套清障、耕地、播种、浇水或收获实现；下一步恢复 `complete_master_angler` 的 Golden Walnut 连续新档证据链。
- 06:00 商店门已在共享 `ShopObjectiveStageCandidates` 路线层修正：跨图候选现在同时合并第一段 connector 与最终商店服务的 `AllowedNow/AllowedToday`、最晚开门、最早关门和等待成本。商店尚未开门时保留“今日稍后可用”，但不得进入 ready-now 或 Grandpa 当前方向绑定；排序器将其延后，不再先走到闭门商店。该修复同时覆盖购买、售卖、动物购买和蟹笼补给，没有新增分支实现。精确受影响回归 `88/88` 通过；一次宽筛另暴露 4 个与本改动无关的既有 fixture 失败（清障 2、特别订单候选身份 1、浇水耗时旧期望 1），未混入本切片。

## 当前增量 · Master Angler 原生竹鱼竿前置闭环

- 当前权威来源是原版 Spring 2 事件链，不是先去 FishShop 购买。鱼竿为空时，统一 Stage One Teacher 依次消费 `spring_2_1` 邮件、基础任务 `13`、实时任务路线和事件 `739330`；高层出口仍是既有 `mail.process_letter`、`quest.advance`、`story.advance_event`，机械层仍只使用共享路线、邮箱/菜单和原生剧情执行器。FishShop 购买只保留恢复兜底，不与原生首发链形成第二套系统。
- `quest.advance` 的路线 provenance 现在会穿过 `DailyPlanCompiler` 保留到机械 connector，回执可逐段验证任务 ID、运行时类型、动作、目标地点、事件、邮件和权威资源键。它仍不在全局策略训练 allowlist；三段路线成功只生成 `verified_evidence_only_policy_option_not_admitted`，不会把局部支撑动作冒充正式策略标签。
- 原生事件先前会在 Teacher 快照与产品派发之间自行推进。根因是 `STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE=true` 仍要求 `Game1.player.CanMove`，而剧情期间该值为 false。Runtime Harness 现只对非节日、未跳过、非玩家控制、无小游戏且可由剧情执行器接管的事件边界追加空闲持有；请求在队列中时继续暂停，`activeStoryEvent` 取得执行租约后恢复。原有事件 ID、位置、命令索引、命令原文和决策响应校验保持严格。
- 隐藏、静音、隔离实跑 `rod-native-chain-v19j-20261025-193000` 从开局基线在同一进程连续完成 `9` 集：邮件 `5`、任务路线 `3`、原生事件 `1`。第 9 集 Teacher 与 Product fresh-before 均为状态哈希 `66afb8fc504b60a584dde33e6cc9f5599185dc3700583e9fff232e004cc1474b`，`product_request_state_hash_drift=false`，事件执行为 `applied/verified`；after snapshot 的 `fishing.rod_inventory` 精确为 `(T)BambooPole / BambooPole / 竹鱼竿`。摘要为 `passed`、`6` 条 Teacher admitted、`3` 条 evidence-only、规范数据集 `6 accepted / 0 rejected`，并按透明库存退出，没有启动正式训练。
- 该切片不提升 19 criterion、11 root method 或 `formal_product_training_authorized`。下一固定施工项是复用现有长跑控制面取得首次登岛、Golden Walnut 来源增量及 First Parrot/Turtle/House 三项原生跃迁，再导出 source schedule 并重建 Master Angler audit。

## 当前增量 · Golden Walnut 新档证据归档

- LiveTrainingLoop 新增显式 `--capture-golden-walnut-fresh-save-evidence`。该模式必须同时提供稳定 `--run-id`、最大 Grandpa 分数目标和 `--snapshot-artifact-mode content_addressed_gzip`；默认训练与普通 smoke 不受影响。
- 第一条 success + fresh episode 在任何耐久写入前必须通过 `initial_boundary`：`full` 快照、day `0`、同一 save/run/state/tick、姜岛与三项鹦鹉状态均未推进，且 `69 keys / 130 walnuts` 权威目录逐行完整、全部 `collected=0`。之后前后快照的内容寻址清单与重写引用的 episode 以原子顺序写到同一 `live-snapshots` 顶层，文件名不会被 rolling retention 正则删除，且继续复用原 `_snapshot-blobs`。没有逐帧扫描，也不会每轮重写不断增长的 JSON 数组。
- 正常退出时，循环在正式训练事务提交**之前**一次扫描耐久 episode 并生成 `golden-walnut-fresh-save-episode-manifest.json`；报告记录捕获开关、manifest 路径与 episode 数。若进程异常退出，可用 `build-golden-walnut-fresh-save-episode-manifest --episode-directory <run>/live-snapshots --schedule-id <id> --run-id <id> --output <path>` 从耐久别名重建，不依赖已被滚动删除的临时文件。
- schedule 读取现同时支持普通 JSON 与 `content_addressed_gzip`，证据根哈希绑定物化后的逻辑 JSON。滚动清理回归证明原 `plan-execution-episode-*` 全部删除后，4 个耐久 episode 仍可重建完整测试时间线；相关定向测试 `16/16`、Bootstrap 构建 `0 warning / 0 error`。
- `Invoke-GoldenWalnutFreshSaveCalibration.ps1` 现只编排两个既有入口：先用 `Invoke-RuntimeFreshSaveSmoke.ps1` 生成并核验原生春 1 存档，再交给 `Invoke-ProductBootstrapCalibration.ps1` 复用现有 Backend、Product Executor、日计划和 LiveTrainingLoop。该路径固定 `skip-training`、`full`、`content_addressed_gzip`、rolling retention 与最大 Grandpa 分数目标；策略校准仍要求完整多候选 trajectory，核桃证据校准则以 verified episode 和耐久 manifest 为退出条件，两种准入不混用。
- 真实隐藏静音运行 `golden-walnut-fresh-save-smoke-20261024-161207` 已生成 `1` 条 verified success+fresh episode、day-0 initial boundary 和 `golden-walnut-fresh-save-episode-manifest.json`。首次运行暴露顺序队列聚合回执把原始 `before_game_tick` 与最后机械步骤的 `effective_before_snapshot_path` 混在一起；聚合层现统一把 state hash、snapshot path 和 tick 重绑到整条 episode 起点，未放宽验证器。修正后报告为 `rows_appended=1`、`verified_actions=1`、manifest episode `1`，定向回归 `23/23`、LiveTrainingLoop Release `0 warning / 0 error`。
- 这只证明真实新档采集入口和首条 episode 的端到端链路，不是连续新档日程，更不是正式训练。首次登岛、三次鹦鹉跃迁、51 核桃来源变化、Gourmand 第 2/3 单及后续 Master Angler prerequisite audit 仍未生成，`formal_product_training_authorized=false` 不变。下一固定切片是在磁盘容量充足的训练位置延长同一新档运行，而不是再增加启动器。
- 首轮排序证据显示捕获脚本使用的 `grandpa.maximum_21` 只通过了捕获校验，却被日目标解析器判为 `not_applicable`，导致 62 个候选全为 `-0.034` 并按 candidate ID 先选 AnimalShop。现已由 `GrandpaEvaluationGoalDefinition` 统一识别权威 ID、战略别名和证据别名，脚本改传权威 ID；状态为 `resolved` 时，LiveTrainingLoop 在编译边界仅保留解析器的精确 `bound_candidate_ids`，不篡改全量排序、分数或结构化策略的绑定内顺序。对同一真实 Spring 1 快照离线回放得到 `raise_friendships / social / 28` 个绑定候选，首个计划动作变为社交路线 connector，不再是无目标商店访问。
- `initial-snapshot.json` 过去经 PowerShell 对象往返重写后仍携带桥接旧哈希，不能被当前 Backend 重放。校准脚本现用 `Invoke-WebRequest` 保留原始 JSON，并以无 BOM UTF-8 直接落盘；`summary.json` 等普通对象文件仍使用结构化写入。该修复只保证后续运行可复现，不追改已有证据。目标别名、绑定过滤、脚本保真与既有 fresh-save 守卫的定向回归共 `164/164` 通过；未启动游戏。下一步仍是在有足够磁盘余量的位置延长真实新档 episode 链，而不是增加候选硬编码。
- 连续链入口已修正为显式 bounded target：Golden Walnut 包装器默认 `RequiredVerifiedActions=4 / MaxAttempts=8`，并把参数传入原 Product Bootstrap；捕获模式达标后强制复用 LiveTrainingLoop 的 native save boundary，按 `SaveSlot` 核验跨日和存档指纹变化。最终验收同时要求 loop report 的 requested/verified 数、耐久 episode 数及 boundary required/verified 全部一致。普通非 Golden Walnut 校准保持默认单动作且不强制边界；该离线门已经由下述 v6 真实运行验证。
- 隐藏静音运行 `golden-walnut-starter-fix-v6-20261024-2140` 已把上述 bounded target 跑成真实连续链：`82` 次 verified Product 执行、`82` 个耐久 episode、`12` 条原生日界/存档回执、`3` 条原生收获回执、作物 Teacher 零重派发完成，最终 manifest 接纳 `82/82`。第一条策略轨迹为 `rewards.claim_starter_gift`；后续执行均保持机械校准角色，没有把 Teacher 展开步骤伪装成新的策略选择。
- 该运行先暴露、后关闭两项运行基础设施问题。开局礼盒候选过去没有写 `AllowedNow/AllowedToday`，会被 Grandpa 绑定层丢弃并回退到社交；现在 ready 礼盒稳定绑定 `earn_money`。另有旧 `dotnet` 进程锁住共享 Release 输出，校准入口现把三个可执行项目发布到运行目录内的独立 artifacts/output；失败日志也随运行保存。磁盘门默认仍为 `8192 MiB`，只有显式参数才能为受控短跑降低，并写入摘要。
- manifest 不再要求相邻 episode 的完整状态哈希逐字相等；只允许同 save/player/day/time、最多 `4` 个 tick 且姜岛访问与完整核桃来源目录完全不变的被动观察间隙。真实场地准备又暴露 `farm_layout` 之前没有 `world_progress`，现已在保持 `32x32` 有界地图的同时加入该证据域；验证器本身没有降级。正式 schedule 导出已在这份 manifest 上运行，并准确拒绝：`Fresh-save episodes do not contain the native first-island-arrival transition.`
- Golden Walnut 长期目标控制面现已落地。显式 `--require-golden-walnut-source-schedule-ready` 只有在 `full + content_addressed_gzip` 证据捕获、日计划与原生存档边界同时启用时才可启动；普通 bounded calibration 默认行为不变。循环从每个 fresh before/after snapshot 严格验证 `game-1.6.15`、同一可读存档、`69 keys / 130 walnuts` 目录身份与逐行原生数量、至少一个真实来源增量，以及首次登岛、First Parrot、Western Turtle、Island House 当前原生终态。作物 Teacher 第一日完成不再耗尽整个长期目标预算，每个有界 handoff 收口后回到现有 Grandpa/日计划重算；若最后一个预算内动作恰好完成里程碑，也会直接读取其 fresh after snapshot，不会先误判 attempt 用尽。`total_days >= 224`、attempt、磁盘余量或连续错误任一先到均失败关闭。里程碑只决定循环何时停止；正常退出仍必须由既有 episode schedule builder 从同一 manifest 重新证明首次登岛和三次鹦鹉均发生过 `false -> true`，验证成功后才写 `golden-walnut-fresh-save-source-schedule.json`，否则整轮报告为 incomplete。长跑包装器现可把原生存档/游戏运行目录与证据输出目录分开；默认路径完全不变，只在空间门要求时显式把大体积证据放到另一驱动器。专项回归通过，尚未启动长期游戏运行。下一固定步骤是在磁盘余量满足 `MinFreeSpaceMb` 后执行一次受限长期新档运行并导出 schedule，再重建 Master Angler prerequisite audit；其后采集 Gourmand 第 2、3 单连续原生回执。
- 首次正式受限长跑 `golden-walnut-source-schedule-v1-20261024-222622` 已实际启动：游戏、原生存档和运行目录留在 E 盘，耐久证据写入 I 盘。它完成两轮作物 Teacher 并推进到 Spring 17（`total_days=16`），随后在仍持有 `(O)472 x14` 时被错误拒绝；根因是 Teacher 把水壶当前 `3/40` 的余水当成了整个生长周期总供给，而运行时早已具备 `CanRefillWateringCanOnTile + BFS` 的原生补水链。修正后的 `farm_site_preparation_context.v2` 在九块完整农场快照中逐格发布原生补水源和可站立事实，Teacher 复用共享 `RouteGridPathfinder` 证明补水连通后按单日水壶容量预算；无补水证据仍按旧规则失败关闭。长跑的五次失败尝试还正确触发 episode 连续性拒绝，但 manifest 构建异常过去会越过报告退出；现在改为稳定的 `golden_walnut_fresh_save_manifest_invalid` 未完成结果。该旧证据链已经存在不可修补间隙，禁止续接或放宽验证器；下一步必须用修正版从新的隔离新档重跑。尚未产生 source schedule，`formal_product_training_authorized=false` 不变。
- v2 隔离诊断已纠正“程序卡在购种”的误判：物化后的 content-addressed 快照从 06:00 连续推进至 07:40，`continuation.quantity=1` 是既定的单份 fresh 购买切片，当前动作只是在等待 Pierre 09:00 开门。完整农场布局显示作物区到补水源的最优路线为 18 步，只被 `(70,23)` 的一棵树隔断。Teacher 现以最少清障数、再最短路的确定性 Dijkstra 生成最多 4 个 `watering_refill_preparation_tiles`；承诺、receipt、run-plan request 与 site preparation 均保持该集合，机械作物批次仍只消费 planting tiles。`executor.clear_obstacle` 复用共享路径与原生工具生命周期，不允许移动器同步改地图，也没有新增平行执行器。
- v3 实跑完成两格补水通道清障/锄地后，播种因旧 `ActiveAdjacentTileAction` 自维护路径状态而触发 `adjacent_action_route_drifted`。相邻动作现统一持有 `ExecutorPathCursor` 并调用 `TryAdvanceExecutorPath`；原生播种、施肥、放置和收获终端逻辑不变。v4 `golden-walnut-source-schedule-v4-20261025-0003` 已真实完成 6 次购种、两格准备清障、`(70,23)` 树木 Axe 清除、清后锄地、`6/6` 播种、`6/6` 首日浇水、原生睡眠/存档边界和次日 `6/6` 浇水。运行随后主动停止，因此不冒充 Golden Walnut source schedule。当前固定下一步为提交该闭环修复后重新启动隔离长期日程，继续首次登岛和 69/130 来源历史。
- v4 的 `55` 个耐久 episode 已在异常退出后由正式 builder 重建为合法 manifest，证明现有前缀未损坏；但停止点位于 Spring 2 浇水后、下一次原生存档边界前，所以禁止把重载存档硬接到该尾部。全新 v5 `golden-walnut-source-schedule-v5-20261025-001624` 随后真实完成礼盒、84 动作 Teacher 派发、6 份购种、返场和场地准备，首批播种却稳定失败为 `adjacent_action_route_drifted`。停止后正式 builder 已从 success-only 耐久别名重建并接纳 `46` 条连续 episode；阻塞播种尾部没有进入 manifest。结构化回执证明普通相邻动作的路径构建仍沿用 `allowRemovableObstacles=true`，会把未编译清障的障碍当成可通行高成本格，而共享执行跟随器随后正确拒绝它。普通相邻动作现固定 `avoidSoftObstacles=true / allowRemovableObstacles=false`；同轮调用审计把浇水、锄地、姜和宠物水碗也改为显式 false，并将 helper 默认值改为 fail-safe false；只有会主动破障的普通矿井/火山战斗显式 true。需清障时仍只允许现有 site-preparation 与 `executor.clear_obstacle` 链处理。源码守卫回归已通过，尚未重新启动游戏验证；下一固定步骤是从全新隔离存档复跑至至少首日播种/浇水和原生存档边界，再继续首次登岛与来源日程。
- v6 `golden-walnut-source-schedule-v6-20261025-004151` 已证明上述 fail-safe 路径不再进入播种穿障，但随机布局的 site-preparation plan 同时含当前可执行步骤和后续不可达步骤时，控制环仍错误要求 `BlockedCount == 0`，在返场后拒绝整个准备阶段。现新增唯一 `CropTeacherSitePreparationFrontier`：先选择现有 plan 的 `direct_till_ready` / `clear_then_fresh_snapshot_rebind` 前沿，每步 fresh rebind；若全部 `already_prepared` 才完成，若未完成且无前沿才保留原原因失败关闭。它不更改 BFS、执行器或动作所有权。v7 `golden-walnut-source-schedule-v7-20261025-005622` 已真实完成场地准备、`6/6` 播种、`6/6` 浇水和 Spring 1→2 原生睡眠/存档边界，关闭 v5/v6 两个失败边界。第二天缺一株来自无稻草人阶段的原生乌鸦损失；六个种植和浇水 item 均有不同目标及原生 verified 回执，既有 bounded Teacher 重派发行为正确。异常停止后 manifest 正式重建并接纳 `57` 条 success-only 连续 episode，仍固定 `training_label_eligible=false`。下一固定步骤是启动全新长期隔离运行，不再扩张作物专用实现，让随机损失经现有重规划收敛后继续首次登岛、核桃来源增量和三项鹦鹉跃迁，最终由既有 builder 导出 source schedule。
- v8 `golden-walnut-source-schedule-v8-20261025-011556` 已越过更多原生日界，并在乌鸦损失后由 Teacher 正确把缺失的 `(O)474` 替换为 `(O)472`；重排计划完成 `9` 个场地准备动作后，在 `Farm(71,20)` 重复锄地。反编译核对证明后台帧的原生 `GetToolLocation()` 会忽略 click target，工具目标必须依赖站位与朝向；旧工具移动在相邻 tile 边缘停下，可能继续命中已处理邻格。当前工具执行已删除手写移动循环，统一复用 `TryAdvanceExecutorPath`，并在出手前只用模拟方向输入居中到站立格；禁止直接修改角色位置。校准脚本现向 runtime 固定传入 `MaxUnverifiedExecutionStreak=8`，同一未验证结果达到上限即失败关闭。v9 `golden-walnut-source-schedule-v9-20261024175054` 有界诊断前缀取得 `73` 个 verified Product actions、`7` 个原生睡眠/存档边界，真实完成购种、返场、连续相邻场地准备、`6/6` 播种浇水及跨日维护，没有 unverified streak 或重复锄地错位。该随机种子没有重现乌鸦损失，且运行在首次登岛前主动停止，因此只关闭后台工具定位边界，不冒充 source schedule；`training_label_eligible=false` 与正式训练授权不变。下一步固定为新的有界 source-schedule 长跑，直接推进首次登岛、核桃来源增量和三项鹦鹉跃迁，不增加作物专用分支。
- v10/v11 将后续停滞定位为剧情事件与活动作物阶段的控制竞争。当前实现把 `priority_story_event` 提升到所有作物阶段之前，中断队列不得写作物 harvest/day-boundary settlement，结束后保留原 commitment 并从 fresh snapshot 继续；无效结构化输入也不再逃逸成未捕获异常。v13 `golden-walnut-source-schedule-v13-20261025-035800` 已取得两轮完整 Teacher run，并在第三轮 Spring 20 Marnie 剧情处真实验证抢占：一次 verified 剧情执行、一次安全 blocked 残留尝试后恢复原 `execute_crop_batch`，完成 7 个机械项和原生睡眠，ledger 仍精确为 `0 harvest / 4 day-boundary`。该运行在验收后主动停止，不冒充 source schedule。
- 长跑空闲阶段现调用既有统一 collection Teacher，而不是继续依赖普通候选池碰运气。它从当前 full snapshot 构建 Master Angler target-date intent，把意图作为 additional candidates 送入现有 baseline ranking，再调用 `CurrentStageOneCollectionTeacherPreferenceBuilder` 和 `TeacherPreferenceQueueLoader`；剧情仍优先，Teacher 非 ready 时回退原日计划。requirement inventory、acquisition lowering、Master Angler window index 与 route timing calibration 四份权威输入均为长期模式必填，缺失即启动失败。真实 v13 快照烟测产生 `35 + 2` 条意图、`297` 个唯一候选和一条 `fishing.catch_fish` pending 队列；同鱼多窗口 candidate ID 碰撞已修复。该链仍为 `skip-training`，下一步是新隔离长跑取得至少一条动态 collection Teacher 原生回执，然后继续首次登岛和核桃 source schedule。
- v14 `golden-walnut-source-schedule-v14-20261025-045000` 已让动态 collection Teacher 在真实 Spring 1 新档连续完成 `FarmHouse -> Farm -> BusStop -> Town -> Beach` 的四段原生 verified 路线，但到达 Beach 后才暴露快照中的 `fishing.rod_inventory` 为空：旧 target-date builder 只校验训练竿兼容性，没有先证明持有任意鱼竿，因而错误发布徒手捕鱼意图并与普通候选往返。现在只有 `rod_inventory` 为可读非空数组时才发布 location/mine 持竿捕鱼意图；缺字段或空数组均失败关闭该类意图，已就绪的 crab pot 与天气支援不受影响。v14 原快照重放结果为 `candidate_count=0 / weather_supply_candidate_count=1`，统一 Teacher 不再选择海滩捕鱼而可继续选择其他 exact collection 候选；完整 Stage One 组合自测通过。四段路线只算 supporting transition，不算 Master Angler 原生捕获终态。
- v15 `golden-walnut-source-schedule-v15-20261025-052000` 已在真实新档证明无鱼竿时不再前往 Beach，并由统一 Teacher 选择 AnimalShop 收集采购候选。开店前每 600 tick 的安全等待能够被 starter gift 原生事件抢占，之后正常进入作物 Teacher；但旧控制面每轮仍重建约 `3.8 MB` 全量 collection ranking。v16 `golden-walnut-source-schedule-v16-20261025-054000` 已复用现有 `SelectedQueueDecisionLease`：首次 Teacher 决策后，后续 fresh snapshot 只做同一候选的 deterministic availability/materialization/compile，不再调用策略模型。4 个完成轮次均保持同一候选、同一 queue index 和 `economy_purchase` continuation；全量 Stage One ranking 仅 1 份，3 个已核对续租轮次的 refresh 均为 `policy_model_invoked=false`，约 `10 KB/轮`，避免约 `11.5 MB` 重复 ranking。下一步继续有界新档长跑，先闭合鱼竿前置，再推进首次登岛、核桃 source schedule 与 Gourmand 后续回执；不得把本次控制层优化记为 Master Angler 原生终态或正式训练授权。
- v15 的冻结 ranking 实际已经含唯一 FishShop 竹鱼竿购买路线，但旧 Master Angler frontier 没有把静态 `rod_equipment_acquisition` 生产者投影成当前 Teacher 先决条件，因而排序仍选了 AnimalShop。现新增单一共享协议，严格绑定既有 `economy.buy_supplies`、`FishShop`、`(T)BambooPole x1`、单价上限 `500g` 及 continuation 身份；没有新增模型 option、商店候选、动作编译器或执行器。透明 `rod_inventory` 为空时，这条现有候选以独立 `hard_prerequisite` 优先进入选择；已有任意合法鱼竿时视为满足，缺失/不可用/畸形字段和多个精确匹配均失败关闭。路线、柜台菜单和终端购买分别要求 fresh 地点、FishShop 非只读菜单和竹鱼竿 `0 -> >=1` 回执。该前置与 72 个鱼种 requirement 分开建模，分母仍为 `72`。独立 Release 构建 `0 warning / 0 error`，聚焦回归现直接覆盖路线、FishShop 菜单、`buy_shop_item -> close_menu` 队列和鱼竿库存 `0 -> 1` 三阶段回执，同时覆盖价格上限、已有鱼竿抑制及错误身份拒绝；fresh-save prerequisite 静态审计通过，最新证据位于 `I:\StardewAITraining\rod-prerequisite-focused-20261025-11`。这只关闭“无鱼竿却不会主动买竿”的确定性派发缺口，尚无 v17 原生购买回执，`formal_product_training_authorized=false` 保持不变。下一固定步骤是隐藏静音运行新的有界 v17，先闭合竹鱼竿路线/菜单/购买，再继续首次登岛、source schedule 与 Gourmand 后续回执。

## 当前切片 · 2026-10-21 Full Shipment 冻结并切换 Master Angler

- `money-crop-native-20261021-v10-continuation-v15-full-chain` 已完成既定的第二条有界 Money 连续段：`26` 条 transition、`8` 个实际作物批次、`8` 次单件投箱、checkpoint `6/6 verified`、pending `0`。购种链精确取得 `8` 份 `(O)475`，源存档哈希保持 `9429c6d01d327111af8c81d0c36f2e318ccc4f1b0ea3094d84e19f25308b22e4`，最终状态哈希为 `bb70acbd0b477b7e14d7fe1b4450ee85292fa29e56de854924e4ee67f1216849`。该有界段没有关闭 Money threshold cell，Money 回到复杂度队列第 11 位。
- 旧 current-source corpus 直接重放因 target-date time/energy budget 与当前确定性编译结果漂移而失败关闭。随后使用 `game-1.6.15` 原始字典、当前 goal dependency、authoritative graph 和当前源码重新生成 requirement inventory、acquisition lowering 及三分区 corpus；没有把旧漂移结果伪装成当前证据。
- Pet Love 三个历史原生运行已按当前 authority 严格重放，无需启动游戏或重采。canonical corpus 为 `ready_split_complete_pet_love_teacher_corpus`，包含 `3 rows / 3 partitions`，Teacher comparison 与 native terminal outcome 的 train、validation、test 均完整；SHA-256 为 `99f885037a21a9ae82865cdeec612c61d5bb8018e4701f1d149f8a4738cfb546`。
- 历史 `snapshot.v1` 的 hash 含现已非语义的 `machine_probe_cache_tick`；适配器改为复用共享 `SnapshotHash.MatchesStateHash`，仅兼容当前/legacy 两种严格哈希，不放宽 schema、版本、身份、原生结果或篡改门。完整 Goal Method coverage 自测以退出码 0 通过，并覆盖 legacy 正例、来源替换/碰撞和篡改负例。
- Pet Love reconciliation 已达到 `4/19 executable、5/19 split-complete、3/19 CoverageGateReady、2/11 root ready` 并冻结；随后 Skull Key 三条真实普通矿井终端 episode 按当前 authority 严格重放。train 第 209 天 EVD-106、validation/test 两个独立第 223 天运行分别绑定第 6、31、4 步的 `claim_skull_key`，没有混入骷髅洞、火山或 Quarry Mine。
- Skull Key canonical corpus 为 `ready_split_complete_skull_key_teacher_corpus`、`3 rows / 3 partitions`，Teacher/native train、validation、test 均完整，SHA-256 `ff2afcb27b6e011c3e9261ad1f086f3934cc7710a59adf8f9de021a721df275a`。历史 snapshot hash 通过共享 current-or-legacy matcher；schema、身份、普通矿井 120 层、原生终态和篡改拒绝均未放宽。
- Skull Key reconciliation 已达到 `4/19 executable、6/19 split-complete、4/19 CoverageGateReady、3/11 root ready` 并冻结。随后复核 Full Shipment 发现 dependency graph 节点已经明确 `full_recurrence_required_for_training=false`，但顶层仍保留“必须采完整 fresh-save 154 项 recurrence”的过时 blocker。
- 当前机器证据重新确认 `154/154` 权威静态需求、`641` 条来源、`26` 个执行签名层、静态 blocker `0`；严格原生 index 为 `26/26 verified、0 missing`，共享 shipping/settlement 通过。完整 154 项 recurrence 仍保留给正常游玩与最终可选验收，不是 Teacher coverage 前置。index SHA-256 为 `e30edb0e6aca07deca484c8b8f1a4437eef68cb01120f13074329d7e2e0830b1`。
- 移除自相矛盾的过时 blocker 后，Full Shipment 为 `executable_frontier + coverage_ready`，blocker/next action 均为空。新 reconciliation 为 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`；文件为 `E:\StardewAI-TestLab\artifacts\full-shipment-topology-current-source-20261021\goal-method-coverage-reconciliation.json`，SHA-256 `71f0e4ed0e7928c1e63f39641f4b90153674f154a575ec331342a3d5241adf43`。完整 Goal Method coverage 自测以退出码 0 通过。
- 正式训练仍为 `Money production cells=0/18`、`formal_product_training_authorized=false`。下一步只处理 `complete_master_angler` 的顶层 dependency、日历窗口与 coverage 接线，复用现有钓鱼/蟹笼执行链和已完整的三分区证据。
- Master Angler 的当前日执行链已闭合：72/72 静态窗口会在 fresh snapshot 上重新核对动态查询、地点/路线/计时、可钓 tile、装备与完整 live spawn outcome；蟹笼也已由唯一模型出口 `fishing.collect_crab_pots` 逐次展开收取、装饵、跨图、放置、制作和原生跨日结算。`executor.craft_crab_pot` 已补 EVD-339 原生个人制作菜单回执，`(O)710` 库存 `0 -> 1`、`timesCrafted 20 -> 21`，不再以调度或单测冒充 runtime evidence。刷新后的 option matrix 为 `235 options / 159 runtime-verified / 66 training-eligible`，该 method 的 `option_blockers=[]`，状态从治理阻塞恢复为 `pending_dependency_expansion`。新 `master_angler_fresh_save_prerequisite_audit.v1` 进一步证明 72/72 都有至少两个**静态日历日**，但明确拒绝把它们冒充新档可达机会。钓鱼等级生产者现已严格复用 `dependency.raise_skill_levels.acquire_fishing_xp` 和唯一 `fishing.catch_fish` 出口，按当前缺鱼所需最高等级、原生 XP 阈值、逐次 fresh 回执及 224 日截止重算；删除任一关键图边会重新标记未绑定。
- 普通矿井钓鱼地点生产者现已复用唯一 `mining.reach_depth` 高层出口，而不是增加第二套矿井规划器。透明路线图会把 `Mine` 大厅的原生 `Mine` 地图动作投影为 `UndergroundMine1`；共享 connector 执行器只在当前位置精确为普通矿井大厅、入口层精确为 1 时放行，并仍由游戏原生 `performAction` 完成进入。矿外候选每次只编译一条路线/入口 primitive，然后用 fresh snapshot 重算；进入普通 MineShaft 后继续使用既有楼梯、竖井、战斗、清障与电梯逻辑。普通矿井、Skull Cavern、Volcano Dungeon 和 Quarry Mine 仍严格分离。Master Angler 当前所需普通矿井目标为 floor `20/60/100`，分别覆盖 Ghostfish/Stonefish/White Algae、Ice Pip、Lava Eel/Cave Jelly。该接线关闭 `mine_access_and_depth`、`mine_depth_or_ginger_island_caldera`、`one_of_advanced_location_access` 三个 fresh-save 前置族；阶段审计哈希为 `3374944677ec627995f61d8f4cd1295ff9d80db687d0af653b1b50c49af2b219`，未绑定族从 `12` 降为 `9`。
- 鱼竿装备生产者已按锁定 `game-1.6.15` 的 `Data/Shops FishShop` 与 `Data/Tools BambooPole` 收敛为一个充分硬前置：`(T)BambooPole x1`、价格上限 `500g`。它复用现有 `economy.buy_supplies` 的滚动商店路线、端点交互、单件购买与 fresh 回执；透明预览、实时菜单和运行时执行共同调用唯一 `AuditedShopPurchasePolicy`，只对竹鱼竿放行非 Object 商品，其他鱼竿、工具、家具、配方及带回调商品继续失败关闭。竹鱼竿足以覆盖原生 72 项分母；后续鱼竿、鱼饵和浮漂只作为效率与随机预算优化，不被错误升级为训练硬门。最新审计哈希为 `3c4016ecdadbc5964147d434578dd86c5ce8498adb39ed1cad48938430508e09`，未绑定族从 `9` 降为 `8`。剩余为蟹笼配方/材料/饵料、沙漠巴士、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽；最后再组合 seed-robust 随机重试与 Year 3 截止证明。全局指标暂不增加。
- 沙漠巴士前置已在同一共享 `executor.traverse_connector` 路径闭合，没有新增模型动作或第二套路由器。透明桥把 BusStop 票机与 Desert 返回触发器投影为 `native_bus_transport`，上游候选在 `ccVault`、Pam/当日自驾、`BusStop.TicketPrice`、余额、坐骑和忙碌状态不满足时直接排除；运行时仅调用锁定 `game-1.6.15` 的原生 `checkAction`、`DesertBus` touch action 与 `Yes` 对话，并验证地点、到达 tile 和钱款变化。Master Angler recurrence 复用既有 `complete_community_center.native_completion` 充分路线与 `fishing.catch_fish` 高层出口，每次只编译一个巴士 primitive 后 fresh 重绑目标。删依赖边或把 connector 篡改为普通 warp 都会失败关闭。离线编译、3 项定向测试和 fresh-save prerequisite 自测已通过；尚未取得本切片的真实游戏运行回执，因此不把 source-locked 实现写成 runtime evidence。最新审计 SHA-256 为 `40e3df4d946f9dbf09cf70c399f994b94e7c3038b619f76e7108e05fccd73890`，未绑定族从 `8` 降为 `7`：蟹笼配方/材料/饵料、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽。全局准入指标仍不增加。
- Rusty Key/下水道前置已组合进 Master Angler，但未把 `obtain_rusty_key` 根的未完成证据抹掉。锁定 `GameLocation.performAction` 的 `EnterSewer` 分支表明：已有 `OpenedSewer` 时一次交互进入；只有 Rusty Key 时首次交互写入该 mail 并显示普通文本，第二次才进入 `Sewer(16,11)`。此前共享 connector 在首次文本后不会重试，现已修成关闭原生文本、确认 mail 回执、仅重置同一 connector 一次，再执行第二次原生交互；无钥匙则在候选层直接排除。依赖图严格引用 `dependency.obtain_rusty_key.has_rusty_key`、其 recurrence/event 关键边和唯一 `fishing.catch_fish` 路由出口，删 Master Angler 目标日期边会失败关闭。定向测试、Runtime Harness/Bootstrap Release 构建和 fresh-save 自测已通过；仍没有新增真实游戏运行回执。最新审计 SHA-256 为 `00042d57ed53321e9b7510aa6dc1d8c5502aa11d16496513ced7146bb4e3f04c`，未绑定族从 `7` 降为 `6`：蟹笼配方/材料/饵料、姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。`obtain_rusty_key` 根本身仍按固定复杂度队列第 6 项完成 60 件捐赠和正式证据；全局指标与训练禁令不变。
- 蟹笼供应族现已在原有生命周期内闭合，没有增加第二个模型动作。隔离的 E 盘存档副本与隐藏静音 full snapshot 证明 FishShop 在钓鱼 `2` 级提供 `(O)685` 鱼饵、单价 `5g`，但尚不出售蟹笼；钓鱼 `3` 级开始提供 `(O)710` 蟹笼、单价 `1500g`，两者均为无限库存且通过既有商店安全策略。`fishing.collect_crab_pots` 现在只在精确缺失蟹笼鱼种的随机容量不足时补设施：能制作则优先复用 `executor.craft_crab_pot`，不能制作才经 FishShop 路线、柜台交互、单件购买、关菜单和 fresh replan 买一个蟹笼；远程空饵蟹笼在背包无任意原生鱼饵时先买一个鱼饵。商店关菜单阶段已补齐 continuation 透传，防止购买成功后丢失原目标。定向回归 `48/48`、Bootstrap Release 构建及防篡改自测通过；本切片的运行时证据只证明实时商店投影，不冒充实际购买回执。最新审计 SHA-256 为 `8cd925d8ede0a0c5d3e5b51817095958ffdd7f5ff100d588400f8dda2f46204b`，未绑定族从 `6` 降为 `5`：姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。`training_label_eligible=false` 与全局准入分子不变。
- 天气族先完成一个不夸大的可复用叶：`dependency.complete_master_angler.consume_owned_rain_totem` 精确绑定已由 EVD-288 隔离原生运行 `4/4` 验证的 `executor.use_rain_totem`，锁定 `(O)681 x1`、location-context 路由、非默认节日、明日最终天气仍为 Rain、原生动画/消耗和 fresh 回执。该 primitive 仍由编译器拥有，不作为策略标签；依赖边只说明“已有图腾可制造明日天气前置并 fresh 重算”。防篡改自测会在有效明日天气改成 Sun 时失败关闭。该阶段天气族状态为 `runtime_consumer_bound_supply_and_scheduler_missing`，审计 SHA-256 为 `7dd897e3cfb2a52a5016c4f342eef003b6e6848f5d12d7d44f46aae2f84468a9`。
- 透明桥随后新增 `player.rain_totem_crafting` 的 full-profile 精确投影：从实时已学配方和锁定 `Data/CraftingRecipes` 读取 `(O)681`，发布原始配方、制作次数、逐槽材料可用性、精确可制作数量、产物容量以及个人背包/Workbench 材料来源。实现把原 Crab Pot 逻辑抽为唯一 `ReadExactSingleOutputCraftingProjection`，两个薄包装共同复用，未形成第二套制作读取。依赖图现以 `dependency.complete_master_angler.rain_totem_crafting_read_authority` 机器化锁定 schema、配方和只读边界，篡改 schema 会失败关闭；天气族状态更新为 `runtime_consumer_and_crafting_read_bound_executor_supply_scheduler_missing`。本切片未启动游戏、未新增原生制作回执，也未接入材料生产或“今晚制作并使用 -> 明日钓鱼”调度，所以缺失族仍为 `5`，最新审计 SHA-256 为 `09d9053c264208f11e95b1080da7b5d4db0958a790aa18d1f2640d76cd4d4748`，训练禁令不变。
- 明日雨窗调度现已闭合到同一个 `fishing.catch_fish` 高层出口。Core 生产器从 fresh snapshot、72/72 哈希锁定窗口索引、精确缺鱼集合、装备、location-context 和 Rain Totem 最终天气投影生成且复验一天期意图；排名 API 以 `additional_candidates` 将当前日捕鱼与明日天气支援在默认候选之外注入，修复旧脚本“先排名、后生成意图”的断链。无图腾时只展开共享原生制作候选，制作后 fresh 重算；已有图腾时直接复用 EVD-288 的 `executor.use_rain_totem`，次日再回到目标日期捕鱼。定向回归 `125/125`、Bootstrap 防篡改审计及 Backend/Bootstrap Release 构建通过。天气族现为 `runtime_consumer_crafting_read_and_scheduler_bound_material_supply_and_native_crafting_receipt_missing`；审计 SHA-256 为 `ee97bff20a4991a8f4db70b6b7cf7e6ad9b1a3749ec89b9a048362c281eb9572`。剩余工作是三种材料生产者与一次原生 Rain Totem 制作回执，未绑定族仍为 `5`，正式训练继续禁止。
- Rain Totem 的材料消费 reservation 与原生制作回执已闭合。候选先保留目标日/location-context 的稳定授权身份，Backend 在日计划编译边界将精确节点、槽位、物品和数量作为一个 portfolio 原子提交；编译器和 dispatch gate 都要求同一 ledger revision 与完整 reservation ID 集合。实时闭环只在原生制作 applied/verified 且 after snapshot fresh 时结算全部三项，任何拒绝或异常都会把 primitive 回执改为 `reservation_settlement_failed`，禁止进入训练。EVD-340 的隐藏静音 E 盘运行实际观察到 `(O)681 0 -> 1`、`timesCrafted 0 -> 1`、原生 `CraftingPage` 材料/产物/任务/成就回调验证全部通过；机械 `executor.craft_rain_totem` 仍不在训练白名单。定向 Core `28/28`、Backend 结算 `17/17`、LiveTrainingLoop 构建零警告零错误。该证据不替代 Axe 升级运行回执、fresh-save 材料可达或 Year 3 截止证明；下一步固定为重建 Master Angler fresh-save prerequisite audit，再按其残余 blocker 收敛截止证明。
- Master Angler fresh-save prerequisite audit 已用当前依赖图重建，并修正了旧审计遗漏的 Rain Totem 觅食 9 级前置。未学配方时，天气支援续程只复用现有 `foraging.chop_wild_tree`：选择一棵当前可达、投影完整且具有正向原生 Foraging XP 的成熟野树，执行一个既有清障 primitive 后 fresh 重算，不增加模型出口。权威图另把 Hardwood reserve、三材料原子 reservation/settlement 与 EVD-340 制作回执拆成独立可篡改节点；任意改动目标等级、工具生命周期或证据 ID 都会重新打开 blocker。天气族现为 `implemented_and_bound`，审计 SHA-256 为 `473be51d47822b9576327c5c6979b2c3b302c5c1828bdd50f7f5cb787e99862d`，未绑定族从 `5` 降为 `4`：姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽。定向 Rain Totem `8/8` 与审计自测通过；正式训练仍为 false。下一固定切片按复杂度从小到大闭合秘密森林/森林农场替代分支，再处理共享 Dark Talisman 的 Mutant Bug Lair 与女巫沼泽，姜岛最后。
- 秘密森林/森林农场替代分支现已闭合。目标日期生成器先按实时 `farm.farm_type_key` 过滤 `Farm_Forest` 数据地点，并把命中的森林农场窗口映射到运行时 `Farm`；其他布局消费全局 `locations.secret_woods_access`，复用唯一 Axe 升级生命周期，精确清除 Forest `(1,6)` 的原生 `ResourceClump(602,2,2)`，fresh 重算后再走原生 Forest→Woods warp。清除动作继续使用既有 `executor.break_current_location_resource_clump`，没有新增模型出口或第二套工具执行器；其注册与时间假设已泛化为受支持的当前地图 ResourceClump，但 EVD-212 仍只覆盖绿雨 `44/46`，新 602 用途在取得真实运行回执前保持 calibration-only、`training_label_eligible=false`。依赖审计严格锁定森林农场替代、钢斧等级 `2`、原木身份、共享执行器和三条 recurrence 边；篡改钢斧门槛会重新打开 blocker。最新审计 SHA-256 为 `78f868edd55d4b5807ad211afc10289811c86473df056df9992996b7cd4f618e`，未绑定族从 `4` 降为 `3`：姜岛、Mutant Bug Lair、女巫沼泽。下一固定切片是合并处理共享 Dark Talisman 前置的 Mutant Bug Lair 与女巫沼泽，姜岛最后；Year 3 截止组合和 seed-robust 随机重试仍在其后，正式训练继续禁止。
- 共享 Dark Talisman 前置现已同时关闭 Mutant Bug Lair 与 Witch Swamp 两族。透明桥实时读取 Railroad `529952/C`、quest `28`、Krobus 的 Sewer 身份、`krobusUnseal`、BugLand `(31,5)` 唯一 `SpecialItem(6)` 宝箱、Railroad `(54,35)` Buildings tile `287`、`witchStatueGone` 与 WitchWarpCave `(4,5)` 的 `MagicWarp WitchSwamp 20 42`。高层出口仍只有 `fishing.catch_fish`：BugLand 目标在原生 Krobus 交互产生 `krobusUnseal` 后停止前置展开；WitchSwamp 目标继续复用同一链，开箱与 Skull Key 共用唯一原生 SpecialItem 两阶段状态机，雕像调用原生 `Railroad.checkAction` 并等待冻结动画结束，所有 primitive 后 fresh 重算。编译器要求专用 profile、精确地图、运行时类型、物品数组、tile/tilesheet 与后置条件，任一漂移失败关闭；代码不直接写 quest、mail 或 talisman 字段。定向回归 `5/5`、Master Angler/route 回归 `16/16`、依赖防篡改自测和 Bootstrap/Runtime 构建均通过；没有启动游戏，因此不把源码锁定与单测冒充真实运行回执。fresh-save prerequisite 审计 SHA-256 为 `74460f8612d92ab09ead68aa2c5aeedd7eaa5aaed8dc1a6ced923f84b5578892`，未绑定族从 `3` 降为 `1`，只剩 `ginger_island_access`。下一固定切片为姜岛访问链，之后组合 Year 3 截止与 seed-robust 随机重试证明；`formal_product_training_authorized=false` 和全局准入分子不变。
- 姜岛访问链已完成第二个代码级切片。透明桥与专用原生交互链此前已覆盖 Willy 邀请、船体三项维修、`1000g` 船票、出航事件/BoatJourney，以及第一只鹦鹉、Western Turtle、Island House、Resort 四个 `ParrotUpgradePerch`；本切片把即时材料与货币缺口接回既有机器收取、保证掉落 Hardwood 的清障、投箱和原生睡眠结算，并把“当前 fresh snapshot 中可证明”的金核桃来源接回唯一既有动作链：金核桃灌木、特殊钓鱼、地面核桃、确定性挖掘点、金椰子预测和田野办公室调查。每次只执行一个原生 primitive，并保留 `fishing.catch_fish` continuation 后 fresh 重算；没有新增第二套采集、钓鱼、挖掘、晶球或田野办公室执行器。四次鹦鹉支出是依次持有 `1 + 10 + 20 + 20`，新档到 Resort 的总依赖为 **51** 个金核桃，不是 21。当前地图无精确生产者时明确失败关闭，不会随机游走冒充证明。姜岛定向回归 `7/7`、Core 测试工程构建 `0 warning / 0 error`；未启动游戏，没有新增原生运行回执。**该族尚未完成**：透明桥仍只对已加载/current-location 来源给出可执行候选，跨地图及未加载地点的全 51 核桃权威来源目录、路线与随机重试预算尚未闭合，因此不得写入 `dependency.complete_master_angler.ginger_island_access` 完成节点，也不得重建为“零未绑定族”的正式审计。下一固定切片只做全 51 核桃来源的跨地图权威投影与逐来源复用；之后才重建 fresh-save prerequisite audit，再组合 Year 3 截止和 seed-robust 随机重试证明。`formal_product_training_authorized=false` 不变。
- 姜岛 51 核桃的**来源能力分母**现已闭合，但尚未升级为正式依赖完成。锁定 1.6.15 的 `IslandHut.ShowNutHint` 分母被整理为 `69` 个来源键、`130` 个核桃；透明桥实时读取 tracker、limited-drop、world flag、master mail，并对普通埋藏点调用当前世界 `IsBuriedNutLocation`，对日记埋藏点核对具体坐标、当前玩家已见日记和 `Diggable`。不购买可选 Field Office Bridge、也不使用 Resort 后置来源时，已经接通既有执行器的来源总量精确为 `51`：灌木、岛屿钓鱼、蚌结石、岛屿作物、固定埋藏点和两处西部日记埋藏点。随机来源每次只做一次原生尝试后读取 fresh limited-drop 计数；固定/日记埋藏点复用唯一 `executor.till_soil`，动作队列按来源键、坐标、剩余量和实时验证状态防漂移。跨地图只走一个已解析 connector 后 fresh 重算。相关回归 `42/42` 通过，未启动游戏、未新增原生运行回执。下一固定切片是建立 `1+10+20+20` 支出顺序、阶段可达来源和随机重试在 Year 3 截止内的机器证明，再重建 Master Angler prerequisite audit；在此之前 `ginger_island_access` 与正式训练仍为未完成。
- `golden_walnut_unlock_schedule_proof.v2` 已把金核桃容量按四个原生解锁阶段机器化，当前已接线来源累计容量为 `7/25/51/51`。随机 Resort 路线仍需 `IslandFishing/MusselStone/IslandFarming` 全部 `15` 个限量核桃及联合 `95%` 的 `69/105/214` 次尝试；首选 `deterministic_gourmand_requests` 改以 House 后三次 Gourmand 请求各 `5` 个确定性核桃补足同一缺口。透明桥、候选、编译、跨图 continuation 与唯一 `executor.advance_gourmand_request` 已闭合；E 盘隐藏隔离运行 `runtime-island-gourmand-smoke-20261023-110034` 通过引介和首单原生事件，EVD-341 记录 request index `0 -> 1`、`GoldenWalnutsFound 0 -> 5`、`IslandGourmand1` tracker、五碎片生成/拾取及互斥锁释放，且生产执行器不直接写进度或奖励。`executor.advance_gourmand_request` 因此升级为 `RuntimeVerified`，动作目录为 `161 five-gate`。新增 `island_gourmand_supply_schedule.v1` 已按锁定 1.6.15 `Data/Crops`、`Data/Shops`、`Data/Objects` 及 `IslandLocation/IslandWest` 原生规则闭合供给：大蒜种子必须在第 2 年春季购入并保留，甜瓜/小麦从第 2 年夏 1 起可购，三株在 IslandWest 全年可种且并行接受浇水/睡眠结算，最长甜瓜为 `12` 夜；成熟作物必须保持站立，不调用收获动作，三次交付继续由 Gourmand 链 fresh 重算。每种采购有独立窗口，标准总价 `130g` 不替代 fresh 商店报价。Resort 目标的 Ginger Island 候选现携带 Turtle `211`、House `223` 的最晚边界；Turtle 尚未完成而当前日超过 `211` 时在候选层直接失败关闭，IslandNorth/IslandWest 等无需 Resort 的目标不受影响。路线证明已新增实际日期组合入口，`211/223` 会关闭选路门而 `212/223` 继续失败；第二、三单连续原生回执仍单独挡住训练门。回归 `26/26` 通过，但默认实际日期仍须由 fresh-save 上游日程给出，故 `route_selection_complete=false`、正式训练仍禁用。下一固定切片是生成并绑定这两个实际日期，不再修改公式或动作；全量 hermetic 自检仍被既有 Money Revenue seed supporting-step fixture 缺失 snapshot path 阻塞。

- `golden_walnut_fresh_save_unlock_timeline_proof.v1` 已把上述“两个实际日期”从裸参数改成可审计的上游日程投影。输入必须是哈希绑定的 `golden_walnut_fresh_save_source_schedule.v1`：包含同一 `grandpa.maximum_21` 新档计划的首次登岛日、逐来源确定性核桃完成日，以及同一计划实际安排的 First Parrot、Western Turtle、Island House 三次鹦鹉交互日。投影只接受当前 51 核桃目录中已绑定动作、非 Resort、非可选桥且 `exact_native_*` 的来源；拒绝未知/重复/超量来源、随机来源，以及前置鹦鹉尚未执行就提前采集的后置来源。它独立计算 First Parrot `1`、Western Turtle 累计 `11`、Island House 累计 `31` 的钱包就绪日，并要求每次计划交互不得早于对应就绪日；最终期限使用计划中的实际交互日，而不是把“当天刚凑够”冒充“当天已经完成交互”。Master Angler 审计可通过 `--golden-walnut-source-schedule` 读取并哈希绑定该产物，路线证明不再把两个手填日期当作正式证据。定向回归现为 `21/21`，Bootstrap Release 构建 `0 warning / 0 error`。仓库尚没有真实新档 source schedule，因此默认审计仍失败关闭；下一固定切片是让上游新档 Teacher/日计划导出这一份来源日程（首先要给出真实首次登岛日），然后重建审计。之后只剩 Gourmand 第 2、3 单连续原生回执，不能提前提升训练准入。
- 上述导出接口现已落地为 `build-golden-walnut-fresh-save-source-schedule --episode-manifest ... --output ...`。输入清单必须来自同一 `run_id`、同一存档且哈希连续的 `plan_execution_episode.v1`；每条记录必须 `success=true` 且拥有 fresh after snapshot。导出器只承认透明桥中确定性来源的 `collected_quantity` 实际增量，并分别要求首次登岛、First Parrot、Western Turtle、Island House 出现原生 `false -> true` 状态跃迁，因此路径候选、路过动作、失败回执和手填日期都不能形成教师标签。产物同时绑定 manifest SHA-256、全部 episode/前后快照的有序证据根、run/save 身份和 episode 数量；旧式无来源日程会被时间线证明拒绝。限量来源跨日增长时只在最近一次已观察增量日登记当前累计量，保持保守而不提前计算核桃。定向回归 `12/12`、Bootstrap Release `0 warning / 0 error`。真实连续新档 episode 清单仍未采集，所以下一固定切片是运行同一 fresh-save Teacher 闭环并生成该清单，再以导出的 schedule 重建 Master Angler 审计；之后采集 Gourmand 第 2、3 单连续原生回执。正式训练继续禁止。
- Rain Totem 即时缺料展开已接回同一个 `fishing.catch_fish` 高层出口。它只接受精确 `709 x1 + 432 x1 + 726 x5` 材料行；缺料时复用现有原生候选收取已完成机器产物、砍伐保证掉落 Hardwood 的树、拾取猪生成的 Truffle，或把 Truffle 投入 Oil Maker，每执行一个机械 primitive 都要求 fresh snapshot 后重新判断。没有新增策略标签、机器系统、采集系统或执行器。松焦油 5 天、猪成熟 10 天、Deluxe Barn/Oil Maker/Tapper 建设及 Hardwood 长期储备仍属于长期 reserve，依赖图明确锁为 `long_horizon_reserve_bound=false`；原生 Rain Totem 制作回执也仍缺失，所以天气族仍未完成、未绑定族仍为 `5`。权威图 SHA-256 为 `c437060b231ca4649c96798dfb521fa1bc7767eb5cf5ee2e7dbbabb28c7ccb17`；Rain Totem 回归 `22/22`、Bootstrap Release `0 warning / 0 error`。本机现存 Master Angler 审计夹具早于 60/2/10 权威字段，不能用于生成新的正式 audit hash；不得把临时补字段夹具当作当前证据。
- Pine Tar 长线储备现已作为天气族的独立部分节点接入，仍只暴露 `fishing.catch_fish`。生产器从哈希锁定的 Stage 1 鱼窗、fresh 缺鱼集合、节日日历、Rain Totem location-context、背包 `(O)726` 和当前 Pine Tapper 到期产出计算最早未来雨窗；新增 Tapper 数量按 `ceil(max(0,5-背包-已在产)/floor(使用前天数/5))` 计算，不再固定建 5 台。透明桥按锁定 `Object.placementAction` 额外核对成熟、非树桩、未占用、可 Tap、夏季临时绿雨树例外，并公开松树 `wild_tree:3:0 -> (O)726 / 5 天` 的精确生产行；执行继续复用通用机器制作、放置、支持账本和 fresh 重规划，每次只补一台。专项回归 `46/46`、Core/TransparentBridge/Bootstrap Release 均通过，未启动游戏。依赖图 SHA-256 为 `e6a4637b5501e12bc4f6e45deacd1b551376e0daad838f84ff1b15787ba4dccc`；`long_horizon_reserve_bound` 仍为 `false`，下一步固定为 Pig/Truffle Oil reserve，其后依次是 Hardwood reserve、材料消费 reservation 和原生 Rain Totem 制作回执。现存正式审计夹具仍缺当前 60/2/10 字段，因此本切片没有伪造新的 audit hash；全局指标和未绑定族 `5` 均不变。
- Pig reserve 的建筑前置不再被目录静默丢弃。`building_construction_catalog` 现同时公开新建与升级项目；升级行逐个绑定实时来源建筑类型和坐标，并锁定反编译确认的 `CarpenterMenu.Action.Upgrade -> ConsumeResources -> upgradeName + daysUntilUpgrade` 原生收据。高层仍只有 `buildings.construct`，底层仍复用 `executor.construct_building`，以 `construction_project_kind` 分支，未增加第二套建筑规划器或执行器。编译时会 fresh 重绑来源类型、坐标、价格、材料和原生契约；运行时点击前再次核对来源建筑，点击后同时验证升级名、倒计时、金钱和材料变化。专项测试 `11/11`、Runtime Harness Release `0 warning / 0 error`，未启动游戏。该切片只解除 Barn -> Big Barn -> Deluxe Barn 的执行硬阻塞，尚未建立 Pig 购买、成熟、出门产 Truffle 与 Oil Maker 的跨日 reserve，因此天气族、依赖图哈希、未绑定族 `5` 和正式训练禁令均不变。
- Pig/Truffle Oil 长线 reserve 已接入同一个未来雨窗意图和 `fishing.catch_fish` 出口。Teacher 按 fresh 状态逐次选择 Barn 新建、Big/Deluxe Barn 原生升级、精确猪舍购买 Pig、等待 10 日成熟、用 `Building.doAction` 原生右键开动物门、建立 Oil Maker、拾取 Truffle、投料或领取 `(O)432`；每个会改变世界的 primitive 后都重新投影，不跨状态边界预编译后续动作。非冬季产出机会、建筑/升级工期和 Pig 成熟期进入使用日前 lead-time，已有按期完成的 Truffle Oil 会抑制重复建设；库存已有 Truffle 时会直接建立 Oil Maker，远程 Farm 地面 Truffle 会先路由采集，不会误判为缺 Pig 产能。动物门透明层公开精确建筑/门坐标，编译器会对建筑身份漂移失败关闭，运行时不直接写 `animalDoorOpen`。`ActionQueueCompilerTests` 定向集合 `159/159`、Core/TransparentBridge/Runtime Harness/Bootstrap Release 均零警告零错误，Master Angler fresh-save 防篡改自测通过，未启动游戏。依赖图 SHA-256 为 `2178040959b142f04b3d5be0795945d0286a05ccfad405fe6c84ba63c54a5fd3`；本机审计报告仍为 partial、未绑定族 `5`，状态精确更新为 `runtime_consumer_crafting_read_scheduler_immediate_supply_pine_tar_and_pig_truffle_capacity_bound_hardwood_reservation_and_native_crafting_receipt_missing`。`long_horizon_reserve_bound=false` 与正式训练禁令不变；下一固定切片是 Hardwood reserve，其后是材料消费 reservation 与原生 Rain Totem 制作回执。
- Hardwood reserve 的现有来源链已接入同一未来雨窗意图。Pine Tar 与 Truffle Oil 已满足后，Teacher 会从 fresh snapshot 选择当前 Farm 的 `ResourceClump`、当前地图保证掉落 `(O)709` 的成熟野树，或先执行去 Farm 的 route connector；每次仅执行一个既有 primitive 后重新规划。树桩 parent `600` 绑定保守最低 `2`，倒木 parent `602` 绑定最低 `8`，来源地点、格点、parent、最低产量和状态都进入 canonical 参数与 fresh 验证。即时缺料链同步复用 `executor.break_farm_resource_clump`，没有平行执行器。当前只可宣称 `ready_hardwood_source_reserve_bound=true`：缺铜斧/钢斧时精确阻塞于 `blocked_required_axe_upgrade_missing`，通用商店购买器又刻意禁止工具与 on-purchase action，因此 `hardwood_tool_upgrade_lifecycle_bound=false`、`hardwood_reserve_bound=false`。依赖图 SHA-256 为 `87f9dacd6ed7f85bd8088514da0d4e21308df9d25f4579490740302b866b0802`；Rain Totem `22/22`、Hardwood `7/7`、图联合回归 `24/24` 均通过且未启动游戏。下一固定切片只做原生工具升级 lifecycle，之后才进入材料消费 reservation 与 Rain Totem 原生制作回执。
- 原生 Axe 升级 lifecycle 已完成代码级纵向闭环，但尚未提升为运行时证据。`player.tool_upgrade` 从实时 `Game1.toolData.ConventionalUpgradeFrom` 与 `ShopBuilder.GetToolUpgradeData` 发布下一等级 Axe、精确来源槽、价格、5 根对应金属锭、Clint/Blacksmith 端点、两日等待和待领取工具；Hardwood reserve 据此只生成一个 `submit_tool_upgrade`、一个 `claim_tool_upgrade` 或显式等待/阻塞阶段。日计划与动作编译器 fresh 重绑全部身份、成本、站位和投影指纹，Runtime Harness 仅通过原生 `Blacksmith` 对话、`ClintUpgrade` ShopMenu 行点击、`Tool.actionWhenPurchased` 及 Blacksmith 原生领取完成动作，不直接写钱、库存、`toolBeingUpgraded` 或升级计时器。隐藏静音 E 盘 full snapshot 已将透明模式刷新为 `177 required / 160 readable / 17 contextual / 0 blocking`；目录为 `238 registered / 240 semantic / 237 compiler-bound / 151 product-executor`。新增链路与目录定向测试 `12/12` 通过，完整 Core 为 `2791/2805`，剩余 14 个是本切片前已存在的其他模块失败。两个新 executor 仍为 `RegisteredOnly`、runtime/output gate 缺失，不得训练；`hardwood_tool_upgrade_lifecycle_bound` 只能解释为“源码锁定的离线实现已接线、原生提交/领取回执待采”。下一固定切片为三种材料的消费 reservation，随后接入一次原生 Rain Totem 制作回执；正式训练禁令不变。

## 已收口 · 2026-10-21 Money 首条完整原生作物生产段

- `money-crop-native-20261021-pricefix-v10` 已从不可变源存档完整执行并通过。运行先用现有 `economy.buy_supplies` 链完成 `21` 个有界原生过渡、精确购买 `5` 份 `(O)474` 花椰菜种子，再完成场地准备、春 6 日播种、连续条件浇水、春 18 日收获、`5` 次单件原生投箱、资源中性回家和唯一原生睡眠。整条 crop lifecycle 为 `48` 条连续 transition、`12` 个实际作物批次；两个无需浇水的批次按透明状态跳过。
- checkpoint v12 已达到 `verified fragments=6 / pending requests=0`。planting layout、seed acquisition、seed reservation、aggregate daily capacity、shared native lifecycle 与 terminal settlement 六个请求全部由当前源码独立重建；output retention 和 settlement capacity 继续作为支持证明。终局 recurrence 从 total day `18` 推进到 `19`（春 20 日 06:00），`money 50 -> 925`、`totalMoneyEarned 0 -> 875`，实际原生收入为 `875g`。
- 源存档目录哈希在运行前后均为 `68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`，隔离 Mod 根目录也未变化。最终透明状态哈希为 `5d0e30548a10e62a94caef5c241bd439ee3fd68dec11aa48e35d757e4d17d058`，最终隔离存档树哈希为 `9429c6d01d327111af8c81d0c36f2e318ccc4f1b0ea3094d84e19f25308b22e4`。睡眠回执包含确认、跨日、原生保存、菜单关闭、世界稳定和非职业升级菜单原生关闭六项原因。
- `money_revenue_crop_native_production_segment_receipt.v1` 已对 v10 做完整深回放。它从 run summary 逐层重建 Teacher 决策、commit receipt、obligation plan、native run plan、checkpoint 和 terminal recurrence，并重新哈希源存档、双 Mod、最终透明快照及最终隔离存档树。回执确认 Teacher 完整候选数 `50`、选中 `(O)474` 共 `5` 格、购种成本 `400g`、保守/实际毛收入 `875g`、净现金收入 `475g`；结果哈希为 `9cb93fde39c45b8e83cc5f541f64b3de7929985381a79620b3d2724acf04f6ec`。
- `money_revenue_crop_production_teacher_comparison_receipt.v1` 已把上述原生结果接回完整 Teacher 分母：`50` 个候选中 `5` 个 admitted、`45` 个带明确原因 blocked，选中候选对另外 `4` 个 admitted 候选形成确定性偏好对。只有选中候选 `crop:474:SeedShop:5` 带原生观测，未执行候选不会伪装成 native outcome。comparison receipt 哈希为 `6ed3e5ab911d6b3c1ce6bca8f3edce7a81de2b6a5ab0293e283792a22874b392`。
- 该结果关闭的是第一条完整生产段及其 segment-level outcome-backed Teacher comparison，不是 Money threshold cell：`875g` 尚未跨越最低 `50,000g` criterion，且 comparison receipt 明确保持 `criterion_local_join_ready=false`。因此 `Money production cells=0/18`、`CoverageGateReady=2/19`、`formal_product_training_authorized=false` 均保持不变。
- 后续 v15 已从该段最终隔离存档闭合下一次 Teacher 边界，满足有界退出条件。Money 现暂停并回到 CURRENT_STATUS_CN.md 第 6 节第 11 位；最终返回时，只有累计原生轨迹真实跨越权威阈值，才接入既有 criterion-local `6 x 3` production join。
- 未来 runner 摘要升级为 `money_crop_native_production_run_summary.v2`，会直接落盘最终快照路径/哈希、最终状态哈希和最终隔离存档树哈希；回执构建器仍兼容已成功的 v1/v10，但 v2 缺失任一最终身份都会失败关闭。当前验证基线：PowerShell AST 通过，Core game-free `455/455`，GoalConditionedBootstrap Release `0 warning / 0 error`。

## 历史记录 · 2026-10-20 Money v22 证据恢复与终端结算

- 首份真实隔离作物运行 `money-crop-native-20261019-v22` 已完成 13 个连续游戏日的机械执行。共享生命周期证明现重放 `48` 条连续 transition，其中作物队列 `12`、作物/睡眠支持路线各 `12`、原生跨日 `12`；验证了 `5` 次播种、`50` 次浇水和 `5` 次收获。逐波留存证明确认 `(O)190` 实际库存从 `0` 增至 `5`，保守毛收入下界为 `1750g`。这些是真实运行 fragment，但尚未形成任一 Money production cell，`Money=0/18`、正式训练仍为 false。
- 运行后证明构建不再强迫重跑游戏。`RuntimeMoneyCropEvidenceFinalization.ps1` 是原 runner 与离线 finalizer 的唯一共享实现；`Finalize-RuntimeMoneyCropNativeProductionEvidence.ps1` 会重新验证 production input manifest、源存档树、执行过的 run plan 和全部既有 proof，再只补 output-retention、fragment index 与 checkpoint。它不启动游戏、不修改存档，也不另写第二套证明语义。
- v22 的 checkpoint v12 当前为 `verified fragments=5 / pending requests=1`。已验证：planting layout、exact seed acquisition、exact seed reservation、aggregate daily capacity、shared native lifecycle；output retention 作为 terminal supporting proof 已重建。seed reservation 由透明库存投影生成精确原子 commit request，再通过共享 `ReservationPortfolioLedgerService.Commit` 重放，未手写最终账本。剩余精确 request 只有 `output-retention-terminal:<wave>`。
- 运输箱终局契约已升级为单波 v3、同日多波共享 v2。两者都显式分离投箱前路线、逐件原生投递、投箱后资源中性回家路线和唯一原生睡眠；回家路线必须由统一队列回执重放并抵达透明 `home_location_id`。在运输箱旁直接构造睡眠、路线夹带资源变化、没有移动 primitive 或睡眠未从回家终态开始均失败关闭。
- 共享作物生命周期已升级为 v4，并为短续跑加入显式 `checkpoint_resumes`。恢复点只允许位于已验证 `native_day_rollover` 之后、下一天 `supporting_route_to_crop` 之前；前后完整透明状态在递归移除运行 ID、隔离路径、读取 tick 与 Movie Theater 投影 fingerprint 后必须完全一致。背包、货币、作物、日期、位置或其他游戏状态任一漂移都会拒绝；普通连续运行仍使用空恢复列表和原有精确 state-hash/tick 链。
- 下一固定顺序：从 v22 已保存的第 18 天原生起点做短 continuation，复用既有收获、单件投箱、回家路线和唯一原生睡眠，生成 settlement-capacity 与 terminal/shared shipping proof。唯一 pending request 清零后，才进入 Teacher comparison 与 criterion-local join。
- PR #337 的两项明确 P1 review debt 已关闭：递归 source-boundary 归一化会移除所有层级的三个非语义 tick 字段并精确报告持久字段漂移；商店 endpoint 与 entrance 的未来营业窗口会在上游 defer 到两者较晚开放时间，不再被永久 block。当前 GitHub 没有新的明确修复 issue；ongoing/deferred/roadmap issue 只保留为约束，不扩张本轮审计。
- 验证：PowerShell AST 通过，Core game-free `447/447`，GoalConditionedBootstrap Release `0 warning / 0 error`，完整 hermetic 自测与 v22 离线 finalizer 均成功。下一步不重跑 13 日轨迹。

## 当前切片 · 2026-10-18 Money 原生作物生产收集器

- `build-money-revenue-crop-production-teacher-decision` 已补上原先缺失的正式 Teacher 决策生成。输入是同一状态边界的 fresh full snapshot 与一组互不重叠的 bounded `farm_layout` snapshots；候选只来自透明桥实时作物目录、实时商店 stock/价格、现有种子、money、物料供给和可准备地块。确定性策略先按保守净收入最大、再按最早终结日、浇水动作数和种子 ID 排序，并输出完整候选评估、选中作物和紧凑布局。架豆、稻田、特殊种植规则和非 `Grab` 收获在各自拓扑/留存证明接入前明确失败关闭，不会被静默遗漏或假设可执行。
- `Initialize-MoneyRevenueCropProductionTeacherDecision.ps1` 负责写入 typed Teacher request 并调用上述构建器；生产承诺与布局不再由调用者手写。`money_revenue_crop_production_decision_request.v2` 绑定 Teacher request、全部 bounded layout snapshots、完整候选评估和选择策略；`money_revenue_crop_production_decision_receipt.v2` 会从 Teacher request 重新生成决定并要求 canonical JSON 完全相等，再按顺序复用唯一的 `CropCommitmentLedgerService.Upsert`。旧 v1 只保留给历史 hermetic fixture，不允许进入当前 production initializer。
- `farm_layout` 的 bounded 场地投影现由 `FarmSitePreparationProjector` 唯一实现。当前地点适配器与 `FarmReadAdapter` 都复用它，因此玩家尚在 `FarmHouse` 时也能对实际 `Game1.getFarm()` 进行目的受限的最多 `32×32` 读取；没有复制第二套地块分类规则，也没有扩大 full snapshot 的扫描面。
- `Initialize-MoneyRevenueCropNativeProduction.ps1` 已把 v2 收据接到唯一生产输入物化链。它拒绝覆盖已有输入目录，先保护源存档目录哈希，再原样提取 `final_ledger`，依次调用既有 obligation-plan 与 native run-plan builder，最后生成 `money_crop_native_production_input_manifest.v1`，绑定源存档目录、Teacher request、Teacher 决策、快照、账本、两级请求和两级计划共 9 个引用制品的绝对路径与 SHA-256。该 manifest 仍不是原生证据或训练授权。
- `Invoke-RuntimeMoneyCropNativeProduction.ps1` 现在只接受上述 production input manifest，不再允许调用方分别传入源存档与 run-plan request。独立验证库会在构建、部署或启动游戏之前复核源存档树哈希、全部 9 个制品哈希、Teacher request/selection policy、决策最终账本的原样提取、各级 path/hash 血缘、固定 Money 目标边界，以及 decision/receipt/obligation/run-plan 四层完全相同且不重复的 commitment 集合；运行器直接复制并消费清单中的既有 run plan，不再重复生成第二份计划。
- `Invoke-RuntimeMoneyCropNativeProduction.ps1` 已把 `money_revenue_crop_native_production_run_plan.v2` 接入隔离源存档、精确双 Mod allowlist、现有 action/plan compiler、RuntimeTestHarness 原生执行和统一回执链。v2 先枚举全部非零种子 shortfall，并复用既有 `economy.buy_supplies` 候选与编译执行链，以一次一份、fresh snapshot rebind 的有界原生切片精确补齐，禁止脚本直接改库存或货币；随后按连续日期逐批执行 `plant -> conditional water -> harvest`，每次跨地图只编译一个透明 connector 并 fresh replan，每个非终端日复用既有 `recovery.stabilize_day` 生成唯一原生睡眠。每项补购现在同时物化 v2 执行清单和可重建证明，直接进入同一 crop checkpoint。
- run plan 现在从权威 crop-production 源快照当天开始，而不是错误地跳到最早种植日；源日到种植日前的每一天只能有一个原生 sleep boundary。运行器在任何动作前输出 `money_crop_native_source_preflight.v1`，要求隔离存档首次 fresh full snapshot 的 `state_hash` 和 `total_days` 同时精确匹配计划源边界；不匹配时立即停止，不能用同日期的相似存档代替。
- 播种日不再假设选中地块已经长期保持 HoeDirt。每个承诺的布局必须能被一个 `32×32` `farm_layout` 投影完整覆盖；运行器在布局与容量取证前调用既有 `money_revenue_crop_site_preparation_plan.v1`，一次只执行一个现有 `clear_obstacle`、`break_farm_resource_clump` 或 `till_soil` 候选，每次状态变化后重新抓取 fresh bounded snapshot 并重建计划，直到全部目标格被透明状态确认为 `already_prepared`。阻塞、候选缺失、日期/地点漂移或有界动作数耗尽都会停止运行，不会直接尝试播种。
- 浇水批次在执行前只按透明 `farm.crops[].needs_watering` 过滤；雨天不生成冗余浇水，但若当天逐日容量证明要求边界快照，仍可通过资源中性的 crop-ingress 到达 Farm。跨地图 move 被现有 compiler 重写为 connector 时现在保留唯一 `candidate_id`，因此路线、作物动作和睡眠的 queue/receipt/after snapshot 都能维持可审计候选身份。
- 当前收集器会物化 seed-acquisition v2、planting-layout、aggregate daily-capacity、shared lifecycle v3、逐波 output-retention、fragment index v13 和 partial checkpoint v12；源存档及隔离 Mod 目录都做前后哈希保护。上方 2026-10-20 切片记录了首份真实 v22 制品；terminal ShopMenu/shipping settlement 尚未闭合，因此 `Money production cells=0/18`、`formal_product_training_authorized=false` 不变。
- 下一固定切片已由 v22 checkpoint 收敛为 seed reservation 与 terminal shipping settlement 两项，再进入 Teacher comparison 与 criterion-local join。不得手写生产 JSON，也不得另写第二套购买器、作物候选、规划器或执行器。

## 已收口 · 2026-10-18 Money 购种原生切片证明 v2

- 旧 v1 只接受单一 acquisition terminal transition，无法表示安全购买链必须执行的“路线/开店 + 多次 quantity-one 购买 + 每步 fresh rebind”。v2 改为读取不可变执行清单，但每条 transition 仍来自现有 `economy.buy_supplies -> DailyPlanCompiler -> ActionQueueCompiler -> RuntimeTestHarness`，没有第二套购买器或执行器。
- 每条回执都经统一 `QueueExecutionReceiptValidator` 重放；支持阶段只允许资源中性的移动、连接器、交互、对话、等待和关菜单，购买阶段只允许等待、单次 `executor.buy_shop_item` 和关菜单。continuation 必须锁定种子、商店、stock row、输出 stack/quality、价格上限和 `quantity=1`；实际库存增量和 money 减量必须与编译参数一致。
- 状态链要求同一 save/player/game、同日、逐 tick 连续且不晚于 seed deadline。首个缺种承诺必须从计划源状态开始；后续缺种承诺必须按计划顺序引用并重建上一份购种 proof，以其终态作为唯一新起点。支持阶段的完整持久资源边界必须不变；购买阶段除玩家库存、money/余额镜像和物料图外的持久状态必须不变。全部切片的累计净增必须精确等于计划 shortfall，少买、超买、断链、跳过前序承诺、错误物品或身份漂移均失败关闭。
- hermetic 自测覆盖一段资源中性路线和两次购买的成功聚合，并拒绝缺最后一片、替换 before boundary 及非单份购买。该 proof 已接入 fragment index/checkpoint；尚未产生正式原生 fragment，所以 Money 仍为 `0/18`，训练授权仍为 false。

## 已收口 · 2026-10-18 Money 整计划共享原生生命周期

- `money_revenue_crop_native_lifecycle_proof.v3` 不再为每个承诺接受一条互相独立的世界时间线。每份逐承诺 proof 都必须从 aggregate daily-capacity proof 加载整份计划的全部布局，并重放同一条连续 world-state/tick 链；每个原生睡眠只出现一次，同时验证全部仍活跃作物的反编译 `Crop.newDay` 转换。v3 进一步承认睡醒位于 `FarmHouse`、睡前又必须回家的真实原生状态：`supporting_route_to_crop` 和 `supporting_route_to_sleep` 分别位于当天作物动作前后，只能复用既有移动/连接器/有界等待 primitive，必须由统一执行回执验真、保持同一日期、保持库存/货币/持久世界状态不变并最终到达声明的 `Farm` 或 home 地点；缺路线、隐藏传送或夹带资源动作均失败关闭。雨天没有实际作物动作时不得生成无依据路线；只有 aggregate daily-capacity 明确要求当天 Farm 边界快照时，才允许资源中性的 crop-ingress。
- 作物动作队列仍复用现有播种、浇水和收获执行器，但每个同类队列必须完整归属于一个不重叠承诺布局。transition proof 记录 `action_commitment_id`；output-retention 只选择本承诺的连续收获批次，因此同日多承诺不会互相吞并库存增量或动作分母。
- fragment index 已升级为 v13、checkpoint 已升级为 v12。hermetic 双承诺正例不再建立两份独立生命周期后合成终点库存，而是在一条共享轨迹中顺序完成 `2+3` 格的全部动作、逐承诺留存、5 次原生投箱与一次共享睡眠结算，并在每天动作前后用两类显式资源中性路线连接 `FarmHouse` 与 `Farm`。正式 fragment 仍未采集，Money 保持 `0/18`。
- 上方收集器已实现该逐日循环。下一固定切片是首次真实隔离运行及 terminal settlement 接线；不得另建作物规划器或动作执行器。

## 已收口 · 2026-10-18 Money 同日多波共享原生结算证明

- 新增的同日多波证明现为 `money_revenue_crop_shared_shipping_settlement_proof.v2`，用于一个收获日存在多个承诺/波次时的真实组合边界。它独立重建作物义务计划、整计划结算容量证明与每份输出留存证明，要求终端 fresh snapshot 在同一存档、玩家和收获日中持有全部保守库存分母。
- 投箱逻辑没有复制：单波 `money_revenue_crop_terminal_settlement_proof.v3` 与共享证明共同调用同一个单件原生投递批次校验器、资源中性路线校验器和 Revenue 睡眠 recurrence 校验器。共享模式先按容量证明固定顺序连续投递全部波次，待结算值必须从 `0` 跨波次严格递增；投递完成后必须沿显式资源中性路线回到透明 home location，最后才允许一次原生睡眠结算。
- 当时 fragment index 升级为 v11、checkpoint 升级为 v10；当前已由共享生命周期及显式睡醒路线切片推进到 v13/v12。共享证明覆盖的每个 `output-retention-terminal:*` ID 才进入 request numerator；留存与聚合容量仍只作支持。单波 proof 与共享 proof 重复覆盖同一 request、重复波次、跨日波次、错误顺序、库存不足或金额链断裂均失败关闭。
- hermetic 正例对同日两个承诺建立 `2+3` 件真实留存支持，执行 5 条连续单件投箱队列并以唯一睡眠结算 `600g`，checkpoint 同时关闭两个终端 request。正式 fragment 仍未采集，所以 Money 保持 `0/18`、`formal_product_training_authorized=false`。下一固定切片是 real Money native production collection runner：从 Teacher 承诺生成有退出条件的逐日队列，复用现有播种/浇水/收获/投箱/睡眠执行器采集上述全部 proof fragment。

## 已收口 · 2026-10-18 Money 作物结算日聚合容量证明

- 新增 `money_revenue_crop_settlement_capacity_proof.v1`。它重建完整作物义务计划、整计划逐日容量证明和全部布局证明，再把不同承诺的保守收获波次按同一 `harvest_total_day` 合并；每个保守单位都必须占用一条只含一次原生单件投递的 fresh-replan 队列。
- 路线和时序没有产生第二套实现：作物维护层公开既有确定性 BFS 的终点；运输候选与容量证明共用 `ShippingBinRouteProjector`；投递编译器与容量证明共用 `ShippingSettlementBudgetPolicy.NativeSingleItemDepositTicks`。聚合日预算由既有维护 ticks、维护终点至运输箱站位的 BFS 尾程和 `N` 次投递 ticks 组成，必须在 autonomous recovery cutoff 前整体可达且不超时。
- 当时 checkpoint fragment index 升级为 v10、checkpoint 升级为 v9；当前已由共享生命周期及显式睡醒路线切片推进到 v13/v12。整计划容量证明仍只作支持，不进入 request denominator，也不增加 numerator。
- 该切片只证明计划可容纳；上方共享结算证明现已补齐多个真实波次连续投递和共享睡眠的组合边界。正式 fragment 尚未采集，Money 仍为 `0/18`，`formal_product_training_authorized=false`。

## 已收口 · 2026-10-18 Money 作物逐波输出留存证明

- 新增 `money_revenue_crop_output_retention_proof.v1`。每份证明只覆盖一个精确承诺的一个收获波次，独立重建作物义务计划与原生生命周期 proof，并只选择该波次连续的既有 `executor.harvest_crop` 队列和统一执行回执；收获动作数必须等于承诺地块总数。
- 留存以透明玩家库存为唯一当前终点：精确 `qualified_item_id` 的库存净增必须不少于计划中的保守最小产量。额外随机产量只记录实际增量，不抬高计划的保守收入下界；地面 debris 不等于玩家已持有库存，也不能通过此证明。
- hermetic 正例覆盖 2 格草莓的 5 个再生收获波次，每波都验证 `2` 个动作与至少 `2` 个实际库存增量；负例保留合法作物收割状态转换但移除最后一波库存增量，证明会失败关闭。镰刀作物若先落为 debris，当前 v1 会失败关闭；后续必须把既有拾取执行与回执接入留存边界，不能把 debris 冒充留存，也不能另建作物执行器。
- fragment index 已升级为 v7，checkpoint 已升级为 v6，并可逐份重建 output-retention fragment。尚未采集正式 fragment，Money 仍为 `0/18`，`formal_product_training_authorized=false`。下一固定切片是把已留存输出接入既有 ShopMenu sale 或 shipping recurrence，证明原生结算与 Money/`totalMoneyEarned` 增量。

## 已收口 · 2026-10-18 Money 原生作物生命周期证明

- 当时新增 `money_revenue_crop_native_lifecycle_proof.v1`，只编排并验证既有 `executor.plant_seed`、`executor.water_crop`、`executor.harvest_crop` 与 `executor.sleep` 队列，不新增作物候选、规划器或执行器。训练队列每批最多 8 项；证明允许连续分批，但要求每批同类、状态哈希连续、执行回执完整，且同一天对承诺布局的播种/收获格必须不重不漏。浇水日要求最终逐格已浇水；雨天已湿地块无需生成冗余动作，显式浇水仍不得重复。当前契约已由上方整计划共享轨迹修正升级为 v2。
- 每个跨日边界必须是唯一原生睡眠队列。验证器从透明桥 `farm.crops` 读取种子、阶段数组、当前阶段、阶段内天数、成熟/再生倒计时、死亡和浇水状态，并逐格重放反编译的 `Crop.newDay` 规则。快照身份、世界日期偏移、队列回执、目标位置和种子身份任何一项漂移都会失败关闭。
- hermetic 正例完整覆盖同一批次 2 格草莓从播种、48 个逐格浇水动作、24 次原生睡眠到 10 个逐格再生收获动作，共 54 条连续队列转换；它同时验证多项目队列的逐项回执链和按批次聚合的种子消耗，缺最后一段生命周期会被拒绝。fragment index 已升级为 v6，checkpoint 已升级为 v5，并能独立重建 lifecycle fragment。
- 该层证明原生生命周期执行，不证明每轮实际产量、产物保留或收入结算。后续逐波 output-retention proof 已实现，但尚未采集生产 fragment，Money 仍为 `0/18`，`formal_product_training_authorized=false`。

## 已收口 · 2026-10-18 Money 作物逐日容量证明

- 新增 `money_revenue_crop_daily_capacity_proof.v1`。每个证明都会重建精确作物义务计划和**全部活动承诺**的布局证明，再按所有承诺的播种、保守浇水和收获日期并集逐日聚合；不同承诺不能分别通过后再在同一天超额占用时间、体力或水量。
- 每一天必须提供同一存档、玩家和游戏版本的 fresh snapshot，透明字段状态必须可读，`total_days` 必须与 year/season/day 保持同一世界日期偏移。容量计算复用共享单源 BFS 距离场、游戏时钟换算和编译器所有的移动、播种、浇水、收获时序；透明桥新增原版作物 `is_raised` 与精确 power-zero 水壶状态。v1 在需要补水路线时失败关闭，不猜测或省略补水。
- hermetic 正例覆盖 25 天、100 格作物：播种日 `100 播种 + 100 浇水`、同日 `100 浇水 + 100 收获` 及最终纯收获日；负例拒绝缺一天、日期字段漂移和 `99/100` 水量。fragment index 已升级为 v5，checkpoint 已升级为 v4。
- 该层只证明计划内逐日维护容量，不证明原生生长生命周期、逐波输出保留、结算或正式训练。后续原生生命周期 proof 已实现，但正式 fragment 仍需真实采集。

## 已收口 · 2026-10-18 Money production 前 correctness 收口

本轮已关闭 **#307**、**#308**、**#309**、**#310** 与 **#311**：

- native outcome summary 升级为 v2，绑定隔离 Mods 根目录运行前后哈希及两个允许模组的目录、UniqueID、版本、manifest 与整目录哈希；回放重新枚举实物，缺失、额外、漂移、错配或历史 v1 均失败关闭。PowerShell 5.1 / C# 哈希一致性已加入 hermetic 测试。
- 共享 raw JSON 提取已改为 Windows PowerShell 5.1 / PowerShell 7 共用的严格词法扫描，目标 ledger 值按原始字节区间写出，不经过对象往返序列化。双壳层实测覆盖嵌套、转义、时区小数以及缺失/重复属性失败关闭；七条生产调用路径不变。
- 种子预留 proof request/proof 升级为 v2。proof 不再接受调用方手写最终 ledger：必须同时提交原子 commit request/result，且验证器会从计划基准 ledger 精确重放 `ReservationPortfolioLedgerService.Commit`，核对 revision、状态哈希、claim ID、数量、槽位、用途和最终 ledger。预留快照不得早于计划源快照，同日还必须保持 tick 单调；伪造账本和早期快照均有负例。
- 作物义务计划升级为 `money_revenue_crop_production_obligation_plan.v2`。已有同承诺种子 reservation 会显式计入 `current_reserved_seed_quantity`，仅未预留余量参与后续承诺分配；已经过去的种植日失败关闭。proof fragment index 升级为 v3，并以精确计划 SHA-256 和当前文件系统路径语义绑定所有 fragment。
- Revenue denominator 现在逐项验证依赖图声明的 `required_field_paths`，缺字段、不可用状态或空值都会阻断相应节点。农场局部读取使用 overflow-safe 32×32 边界和不会回绕的遍历；资源团块站位统一复用共享 BFS；锄地候选和编译在体力缺失或 `<=0` 时失败关闭。
- terminal-proof collection request/plan 升级为 v2，显式绑定期望 run/partition、骨架分母、concrete denominator 与请求分母。合法 skeleton 可以展开为多个 concrete request；错误 catalog/run/partition/boundary 失败关闭。唯一离线复用 sale episode 的 before capture 现严格使用 request/verifier 的 terminal snapshot。
- 验证通过：Core game-free `372/372`、Bootstrap Release `0 warning / 0 error`、完整 hermetic 自测、E 盘隔离游戏依赖下 TransparentBridge Release `0 warning / 0 error`。未启动游戏，Money production cells 仍为 `0/18`，formal training 仍为 false。

当前已知 P0/P1 review debt 清零，唯一 primary item 继续是 **Money / Revenue**。购种 shortfall 与逐日容量 proof 适配器已经落地；下一切片是原生作物生命周期，然后依次闭合逐波输出保留与结算，再进入真实 Money native production collection、terminal proof completion、Teacher comparison 与 criterion-local join。**不得因为已有 terminal/crop infrastructure 就提前把 Money 从 `0/18` 上调。** Full Shipment 已冻结为 current-authority `26/26`，不再作为当前施工项。

## 已收口 · 2026-10-18 Money 作物种子短缺获取证明

- 历史 `money_revenue_crop_seed_acquisition_proof.v1` 只适配既有 `AcquisitionRoutePortfolioRolloutProofBuilder` 的单 terminal rollout，现已由上方 v2 连续原生切片证明取代；该历史实现同样没有新增商店候选、寻路、购买编译器或执行器。
- proof 强制 rollout 从精确 crop-plan snapshot 起步，`goal_id=earn_money`，只有一个完成的 terminal transition，物品等于承诺种子、`required_amount` 和实际增量都等于计划 shortfall，并在 seed-supply deadline 当日或之前完成。同存档/玩家、状态哈希、文件哈希和 fresh terminal receipt 均独立核验。
- crop fragment index 当时升级为 v4，checkpoint 升级为 v3，可累积该 proof；少买一份、错误计划边界、额外 continuation 或未完成 rollout 均失败关闭。后续逐日容量切片已把它们分别升级为 v5/v4。尚未采集正式购种 fragment，Money 仍为 `0/18`，正式训练仍为 false。

## 已收口 · 2026-10-18 Money 种子预留 proof

- `build-money-revenue-crop-seed-reservation-proof` 已能重建义务计划，并核对同一 save/player、透明物料图、活动 reservation、种子身份、状态哈希、库存槽与完整数量。
- #310 已关闭：committed reservation ledger 必须由原 crop plan 的 strategy-ledger lineage 经原子提交服务合法演化，reservation snapshot 具备 post-plan freshness 下界。
- seed-reservation fragment 已通过 correctness gate；购种 shortfall 与逐日容量已有精确 proof 类型但正式 fragment 仍待采集，生命周期、输出保留与结算继续 pending。Money 保持 `0/18`，训练授权仍为 false。
## 已收口 · 2026-10-18 Money 作物场地准备计划

- 透明桥已有目的受限的 `farm_layout` 投影，计划器也已复用现有 `executor.clear_obstacle`、`executor.break_farm_resource_clump`、`executor.till_soil` 候选；清障后必须取得 fresh snapshot 再绑定锄地。
- #311 已关闭：局部边界与遍历防整数回绕，resource-clump 站位统一使用共享 BFS，zero-stamina 与缺失 stamina 均在候选和编译阶段失败关闭；crop denominator 的 required fields、过期种植日、已有预留和精确计划哈希也已收口。
- 该层仍只是机械执行计划，不是 planting-layout proof、生产证据或训练授权；Money 保持 `0/18`。
- 当前原生作物运行器已消费该计划：播种日按一个候选一次执行并在每次清障/锄地后 fresh rebind，完成后才捕获 planting-layout 与 aggregate daily-capacity 边界。场地准备动作本身仍不增加证据分子，也不改变训练授权。
## 历史记录 · 2026-10-16 Money 作物布局证明检查点

- `money_revenue_crop_production_obligation_plan.v1` 的每项承诺现显式保留种植季节、基础生长天数，并把 `planting-layout:<commitment>` 纳入同一个证据分母。旧计划缺字段或请求分母漂移时必须重建，不能沿用。
- 新增 `build-money-revenue-crop-planting-layout-proof`。调用方提供的是非机械的精确地块选择；构建器逐项重放原计划和 planting-day 透明快照，核对同一 save/player、精确日期、Farm 室外季节、空且无对象的非花盆 HoeDirt、原生种植许可、季末成熟许可及不晚于承诺上界的原生调整后生长日数。实际播种继续复用 `farm.maintain_crops -> executor.plant_seed`，没有第二套作物执行器。
- 新增 `money_revenue_crop_production_proof_fragment_index.v1` 与可恢复 checkpoint。v1 只认可重新构建一致的布局 proof；fixture 中 `1/10 verified` 时其余种子预留/购种、日容量、原生命周期和逐波终端保留请求全部保持 pending，三个训练/日程 gate 均保持 false。分母外、重复、篡改或不可用地块均失败关闭。
- 这一步同时暴露真实后续缺口：如果种植日没有足量合法 HoeDirt，proof 不会假装可执行。下一切片先把已有清障、锄地和路线动作组合为前置场地准备计划/证明，再闭合种子供给、逐日时间体力容量、生命周期和输出保留；Money 生产仍为 `0/18`。

## 历史记录 · 2026-10-16 Money 未来作物生产义务计划

- 既有 `CropCommitmentLedgerService` 现在从透明 `farm.crop_catalog` 为每项种植承诺固定种子身份、是否需浇水、逐日浇水义务、每次收获日、最小波次数、最小产量、零品质单价和保守毛收入；所有日期和数量由共享 `CropProductionObligationProjector` 计算，没有另建作物规划器或执行器。
- 新增 `money_revenue_crop_production_obligation_plan.v1`。它重新核验快照、策略账本、原生作物目录和当前未预留种子库存，为每项活跃承诺生成购种/预留、日容量、原生成长以及每个收获波次的终端保留证据请求；同一批种子不能被多个承诺重复计算。
- 旧版、字段缺失或被篡改的承诺失败关闭。计划固定保持 `future_crop_production_proof_ready=false`、`fresh_save_revenue_schedule_ready=false` 与 `formal_product_training_authorized=false`；它关闭的是未来作物义务遗漏和采证分母不明确，不是生产证据本身，Money 仍为 `0/18`。
- 下一切片只沿该计划补可恢复的原生 proof checkpoint：验证购种/预留、每日时间与体力容量、作物生命周期和每波输出保留，再由 fresh-save 下界日程组合到百万元目标。验证已通过 Core game-free `366/366`、Backend `229/229`、Bootstrap Release 零警告及 hermetic 关键链，未启动游戏。

## 历史记录 · 2026-10-16 Money terminal-proof 可恢复 checkpoint

- 新增 `money_revenue_terminal_proof_fragment_index.v1` 与 checkpoint builder。fragment 可以分批累积，但每一项必须在计数前通过既有 terminal execution verifier；重复 request、denominator 外 request、上游计划漂移、账本/快照/recurrence 篡改都会失败关闭。
- `Initialize-MoneyRevenueTerminalProofCollection.ps1` 自动物化 collection plan 中唯一的 `reuse_verified_shop_sale_episode`：不启动游戏，只使用已验证 episode 的真实 before/after snapshot 和 run ID，生成独立前后 strategy-ledger capture 与 Revenue recurrence，再进入 checkpoint 深验。脚本可重入，已有 fragment 先验证后保留。
- partial checkpoint 只报告 verified/pending request IDs，不能进入 completion。`build-money-revenue-terminal-proof-index` 仅在 `pending=0`、verified count 等于完整 request denominator 且 checkpoint 精确重建一致时输出 v1 final index；formal training 始终保持 false。
- hermetic 回归覆盖完整 fragment、空 checkpoint、重复 fragment 拒绝与 final-index gate。当前只关闭“已有原生 episode 仍需人工手拼 proof”的缺口；其他 shop/shipping/rebound 反事实仍需后续原生采集适配，Money 仍为 `0/18`。

## 历史记录 · 2026-10-16 Money terminal-proof 采集适配

- `Initialize-MoneyRevenueComparisonWork.ps1` 在 concrete denominator 与 counterfactual request catalog 完成后，现自动调用 `build-money-revenue-terminal-proof-collection-plan`，不再把“下一步如何采证”留给调用者从 catalog 手工猜测。
- 新计划重建 comparison plan 和 counterfactual catalog，保持一请求一工作项。每项固定列出 terminal snapshot/candidate/strategy-ledger 身份、原生收入证明种类和有序阶段；acquisition rebound 会显式要求恢复已验证终态，shop 与 shipping 进入不同采集模式。
- 唯一已有原生 sale episode 只有在 source state、catalog candidate、shop、item、slot、quantity 和 unit price 全部匹配时才标记为 `offline_materialization_ready`。其余项保持 `native_collection_required`；计划固定 `terminal_proof_index_ready=false`、`formal_product_training_authorized=false`，没有把计划伪装成证据。
- Bootstrap hermetic 新增三路分流和重复 request 拒绝回归；后续可恢复 checkpoint 已按上节落地。

## 历史记录 · 2026-10-16 Money 作物生产支持边界修正

- 首次真实 Money 原生采证诊断暴露的“`farm.maintain_crops` 需要进入训练 allowlist”是旧依赖图中的层级错误，不是动作缺失。EVD-226 已闭合该高层选项的五道证据门，但其职责是把透明的当前地点作物义务确定性展开为浇水、播种、施肥、普通收获或巨型作物收获；它不得成为 Student 策略标签。
- `earn_money` 依赖图现在把 `farm.maintain_crops` 接成 `deterministic_transition`：Teacher 负责选择作物收益方法及经济参数，编译器负责逐 fresh snapshot 展开机械动作，只有验证过的原生收获回执才能流入 reserve-safe 待售库存。当前快照 denominator 会枚举该支持节点，但不会把它计入 comparison candidate。
- 原错误 blocker 已替换为真实剩余工作：未来日期的购种、播种、成长、浇水、收获和库存保留承诺仍需完整证明。fresh-save 百万累计收入下界日程和反事实机会成本标签也仍未闭合，所以 Money 保持 `0/18`，`formal_product_training_authorized=false`。
- 新增权威 JSON 架构回归，固定作物节点类型、五种候选 kind、运行时证据状态、非训练准入状态和图连接；bootstrap 自测同时证明确定性作物候选不会泄漏到 Teacher 比较标签。验证为 Core game-free `356/356`、Backend `229/229`、Bootstrap Release `0 warning / 0 error`、hermetic 自测与当前 checkpoint 检查通过，未启动游戏。

## 历史记录 · 2026-10-16 current-authority Full Shipment 26/26 收口

- 从锁定的 `9/26` 检查点按冻结顺序完成剩余 17 个标准 strata。最终里程碑检查点为 `26 records / 0 remaining / completed_selected_batch`，严格 evidence index 为 `verified=26 / missing=0 / shared_shipping=true`；原生游戏、Backend 与 Product 进程均已退出。
- 首次防风草运行被精确重放守卫拒绝。根因是 PowerShell 把后端回执内的 ledger 历史时间从 `+00:00` 往返序列化为本机 `+08:00`，语义时刻相同但 canonical JSON 不同。没有放宽 verifier，也没有改写旧证明。
- 新增共享 `Write-RawJsonProperty`，标准提交/结算、断点恢复结算、规划重建提交以及商店提交/支持提交/结算共七个路径统一直接从后端原始响应提取 `ledger`。全仓运行脚本已无 `.Value.ledger` 的有损提取，源守卫固定该约束。
- 修复后真实防风草路线依次通过 atomic commit、原生采收、fresh terminal、settlement 与 rollout proof；其余 16 个标准层连续通过同一链，证明修复覆盖真实闭环而非只通过离线测试。
- 收尾验证为完整 solution Release `0 warning / 0 error`、Core `352/352`、Backend `229/229`；Full Shipment settlement、dispatch、static compilability、runtime sample evidence、Bootstrap batch 与 hermetic 自测全部通过，变更脚本 AST 和 `git diff --check` 通过。
- 该结果只完成 Full Shipment 的当前权威运行签名层。`formal_product_training_authorized=false` 保持不变，Goal Method / formal coverage 分母没有增加；收尾验证和提交完成后返回 Money / Revenue WIP=1，不启动训练。

## 历史记录 · 2026-10-16 current-authority 批处理与规划流水线优化

- 固定顺序小批次连续完成 `location_fish_spawn_sample`、`location_artifact_spot_sample`、`geode_drop_sample` 与 `shop_purchase_sample`；优化完成后又以 `community_center_reward_sample` 做真实原生代表回归。五层均通过当前 Teacher、原生执行、运输箱、睡眠结算与 rollout proof；current-authority evidence index 从 `4/26` 推进到 `9/26`，剩余 `17` 层，`formal_product_training_authorized=false`。
- 性能审查确认同一套 calendar + 12 个 target-date 轴此前在首次规划、商店支持过渡重规划和断点恢复重建中各维护一份，并且每个轴启动一个独立 Bootstrap 进程。现统一为 `RuntimeAcquisitionPlanningPipeline.ps1`，14 个确定性命令在一个有界顺序批处理中执行；只有显式标记为 `BatchSafe` 的纯规划构建命令可进入共享会话，失败即停、禁止递归、拒绝重复或覆盖控制文件的输出，且保留逐进程 oracle。
- 使用刚通过的地点鱼真实输入做等价对照：批处理 `14.51s`，逐进程 oracle `93.69s`，14/14 输出 SHA-256 完全一致；紧凑 JSON 将该段落盘量从约 `145 MiB` 降到 `72.5 MiB`。随后社区中心奖励真实原生场景通过同一优化链并进入当前 evidence index。这只消除重复进程、重复读取和空白格式，不改变候选、计划、编译、执行、证明或训练准入语义。
- evidence index 的实测 trace 还暴露默认 32 MiB read session 在 9 份 proof 上产生约 `13.4 GiB` 物理读取和 `3204` 次逐出。索引调用点现单独使用 `256 MiB` 总预算与 `64 MiB` 单项预算；同一输入 A/B 输出逐字节一致，墙钟由 `106.7s` 降至 `92.9s`、物理读取降至约 `10.57 GiB`，峰值工作集由约 `1127 MiB` 增至 `1327 MiB`。确定性构建缓存不扩容，因为 512 MiB A/B 没有增加命中数。
- 全量 correctness 审查同时关闭三个独立缺口：放射性矿石当前候选只接受 `native_radioactive_ore_node / GameLocation.breakStone / (O)909`，普通滚动矿点显式排除怪物掉落与放射性矿点；legacy unbound 证明回退到唯一候选时会保留真实 `candidate_id`；Money 空初始化在 canonical corpus 或非零计划已存在时于任何写入前失败关闭，不能把真实进度覆盖成 `0/18`。
- 回归结果为完整 solution Release `0 warning / 0 error`、Core game-free `351/351`、Backend `229/229`，五项聚焦 Bootstrap 自测与 PowerShell AST 均通过。Money 防覆盖的功能测试证明 fresh 空计划仍为 `0/18 + 3` 个 native waves，已有 corpus 时请求/计划均不落盘且 corpus SHA-256 不变。
- 全仓检查确认旧性能分支的核心观测/缓存能力已经被后续主线实现覆盖，不能直接合入其旧业务修复。当前剩余结构性热点是 2,000 行以上的运行烟测编排脚本；它们应按 fixture/setup、planning、native execution、proof 四个责任边界渐进拆分，不与本轮 correctness/performance 修改捆绑。

## 历史记录 · 2026-10-15 current-authority 高风险小批次推进

- 放射性矿石修复合入后，milestone 按冻结顺序继续运行 `wild_tree_tapper_output_sample` 与 `monster_drop_sample`。两层均直接通过当前 Teacher、dispatcher、防篡改候选重建、原生动作、发货、睡眠结算与 rollout proof，未新增候选、编译器或执行器。
- 怪物掉落仍走 `mining.reach_depth -> CollectMonsterDrop -> executor.combat_monster -> executor.pickup_debris`，证明放射性矿石新增的 `CollectResourceOrArtifact` 分流没有回退现有战斗链。当前 evidence index 为 `4/26`，剩余 `22` 层，正式训练继续失败关闭。

## 历史记录 · 2026-10-15 current-authority 放射性矿石原生重验

- `radioactive_ore_node_sample` 的实时夹具已在普通矿井 99 层生成精确 `(O)95` 节点与保证掉落 `(O)909`。首次 current-authority 运行暴露两处同一链路缺口：`mining.reach_depth` 未携带精确采集意图时被现成梯子抢占；dispatcher 从同状态重建候选时又只为怪物掉落保留采集意图。
- 修复继续复用唯一的 `MiningFloorStepPlanner -> executor.mine_stone -> executor.pickup_debris` 链。放射性矿石意图映射到既有 `CollectResourceOrArtifact` 目标，按 `native_radioactive_ore_node / GameLocation.breakStone / (O)909` 绑定；防篡改重建和 selection 都要求同一组三字段，未跳过候选证据比较。
- 隐藏、静音、E 盘隔离运行 `runtime-full-shipment-evidence-radioactive_ore_node_sample-20261015-204018` 已完成原生破坏、延迟掉落拾取、运输箱、睡眠结算和 rollout proof。当前 evidence index 为 `2/26`，剩余 `24` 层；shared shipping evidence 保持有效，`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-15 current-authority Sap 原生重验

- 当前预算与位置协议合入后，E 盘 9 类旧 `passed` 摘要均由当前严格验证器重新判定为 stale，不能作为 current-authority 证据；旧 Sap 证明也命中 daily time/energy drift。
- 新原生运行 `runtime-full-shipment-sap-current-authority-v7-20261015` 暴露并修复唯一共享 `executor.pickup_debris` 的两个执行问题：可进入掉落格必须真正走入；不可进入格旁必须保持朝向输入以进入原生磁吸范围；BFS 不得规划穿过本动作不会清除的可移除障碍。
- 修复后的同一运行原生获得并运输 `6` 个 `(O)92`，完成睡眠结算和 recurrence proof。当前源码 evidence index 精确验证 `native_wild_tree_chop_drop` 为 `1/26`，shared shipping evidence 为真；其余 `25` 层仍须重新执行原生动作，formal training 仍为 `false`。

## 历史记录 · 2026-10-15 #274 当前权威 force-full 收口

- 对冻结的 Full Shipment `26` 层证据执行当前源码扫描：Sap 的当前重建证明通过，其余 `25` 层分别因 location route、daily time/energy、calendar 或 Teacher preference 漂移而失效。离线 planning 重建已能重新通过 Teacher、commit 与 dispatch，但旧 execution receipt 缺少当前动作队列新增的玩家位置/tile 上下文，因此必须原生重跑，不能改写历史收据或放宽相等性。
- 修正当前权威重建链的真实缺口：shipping deposit verifier 使用现行 `target_location/target_tile_x/target_tile_y` 协议；仅在“当前矿层 + 精确站位 + 候选保守最大移动格数 + 计时校准”全部成立时，允许 purpose-limited mining snapshot 不携带全局 social route graph；跨图或其他缺证据仍失败关闭。
- recovery settlement 的持久状态投影现忽略明确的瞬时定位/缓存字段，同时保留未知持久字段失败关闭。自测临时工件改为进程级隔离，并修复 continuation 夹具的占位哈希、空出货端点及固定 20 金出售协议；这些修正使完整 Stage-One fixture 首次在当前严格哈希/原生收入链上通过。
- 验证通过：Bootstrap Release 构建、bootstrap hermetic、acquisition dispatch、Full Shipment 静态可编译性、完整 Stage-One、Core game-free `350/350`、Backend `229/229`、KnowledgeCompiler/LiveTrainingLoop Release 构建。未启动游戏；current-authority 仍为 `1/26`，#274 和正式训练准入保持关闭。

## 历史记录 · 2026-10-15 rollout proof 单次重验

- `AcquisitionRoutePortfolioRolloutProofBuilder.BuildVerifiedReceipt` 从 observe-only 改为 invocation-scoped deterministic certificate。身份覆盖 proof manifest、初始/续段 execution inputs、支持过渡、checkpoint、执行、终态和结算文件；任一传递内容漂移都会产生新键并重新完整验证。
- 缓存不保留整棵 mutable checkpoint graph，只保存 proof receipt、末端 checkpoint 路径与哈希。每个 consumer 仍重新读取独立末端 checkpoint 对象并核对状态、ledger、transition count 与完成状态；禁用 deterministic session 时恢复完整重验。
- Full Shipment runtime evidence index 增加 exact manifest/receipt content identity 的 unique-proof-first 映射。同一 proof 可服务多个合法 stratum，但每个 route occurrence、authority hash 与 stratum 归属仍逐项验证，26 层分母不合并。
- 该切片只完成 #274 的 rollout single-computation / unique-proof-first 核心，不代表全仓性能验收完成。canonical equality、缓存预算、表示生命周期与真实 A/B/C baseline 仍待后续；Money 仍为 `0/18`，正式训练仍为 `false`。

## 历史记录 · 2026-10-15 Money 首轮 work plan 初始化

- 新增 `Initialize-MoneyThresholdProductionWorkPlan.ps1`，不再要求人工书写首个空 work-plan request。它只调用现有权威 builder，并固定校验 `6` 个 criterion、`3` 个 partition、`18` 格分母和 `formal_product_training_authorized=false`。
- 未提供 corpus 时，输出必须是 native/comparison/complete 均为 `0`、原生波次恰为 `3`、comparison 波次为 `0`；提供 corpus 时仍由原 builder 完整重放验证。脚本不创建伪空 corpus，也不启动游戏。
- 该计划只用于给 `Invoke-MoneyThresholdNativeCollectionWave.ps1` 提供首轮调度；第一条 accepted native run 后，后续唯一输入改为 batch updater 生成的刷新计划。当前生产状态仍为 `0/18`。

## 历史记录 · 2026-10-15 Money comparison 波次身份交接

- 新增 `Initialize-MoneyRevenueComparisonWave.ps1`，用 `dataset_partition + source_row_id` 从 canonical work plan 定位唯一 comparison wave，核对 corpus row、source ID、native run summary 和缺失 criterion，再复用既有 `Initialize-MoneyRevenueComparisonWork.ps1`。不再人工搜索 summary 或抄 criterion。
- 新的 `money_revenue_comparison_wave_handoff.v1` 绑定 source work plan、corpus、run summary、comparison plan 的绝对路径与 SHA-256，以及 row/source/partition/criterion identity。可变 canonical work plan/corpus 会先复制并核对到 comparison work directory，后续其他波次刷新 batch 不会使在途 handoff 失效；completion 使用前仍重新哈希，漂移即失败关闭。
- `Complete-MoneyRevenueComparisonWork.ps1` 在提供 handoff 时只生成该波仍缺的 admission；可选 existing index 会按 `run_id + criterion_id` 精确归并为累计 v3 index，重复 key、非法 outcome override 或版本漂移均拒绝。它仍要求真实 rollout、ledger 和 terminal proof，不改变 `Money production evidence cells: 0/18` 或训练授权。

## 历史记录 · 2026-10-15 Money 原生波次自适应执行

- 新增 `build-money-threshold-native-outcome-wave-selection`：它先校验固定 `18` 格 work plan 和目标分区波次，再从实时权威 Revenue denominator 中选择当前可达的最高缺失阈值；最终动作仍由原有单阈值 selector、daily-plan compiler 与 executor 完成，没有第二套执行链。
- 计划中的最高阈值只表示本波理论上界，不再被误当成当前存档一定可达。源累计收入必须低于最低缺失阈值；候选只能关闭真实 crossed 的连续前缀，更高阈值留给 batch 刷新后的后续波次。若没有任何候选能推进，动作入队前失败关闭。
- 新增 `Invoke-MoneyThresholdNativeCollectionWave.ps1`，负责读取一个当前 work plan、调用既有隔离采集器、重建 canonical batch/work plan，并验证至少一个计划格由真实 native row 关闭。它不生成标签、不把计划当证据，也不改变 `Money production evidence cells: 0/18` 或正式训练状态。本轮未启动游戏或本地高负载测试。

## 历史记录 · 2026-10-15 Money 生产采证波次收敛

- `money_threshold_evidence_work_plan.v1` 继续保留六个权威阈值乘三个固定分区的 `18` 格分母，但新增从同一 verified corpus 派生的采集波次。原生波次按分区聚合尚缺 outcome 的阈值；比较波次按一条既有 native row 聚合尚缺 exact comparison 的 criterion。
- 空生产 corpus 不再被误读为 18 次游戏启动，而是显示 `3` 个条件性原生波次：每个分区各一波。单波只有在源累计收入低于该分区最低缺失阈值、且一个权威 Revenue outcome 到达最高缺失阈值时才可同时关闭多格；选择器仍逐项证明真实 crossed criterion。若当前候选不能满足上下界，必须拆波并重建计划，不能合并或伪造证据。
- 已有 native row 时，比较波次只复用一次 Revenue denominator 初始化，随后仍为列出的每个 criterion 建立独立 comparison admission。同一个 seed 的全部 admission 必须选择同一个 native outcome；若不同 criterion 的唯一优选不同，必须使用独立 seed run。
- 此调度压缩不改变 `Money production evidence cells: 0/18`，也不授权正式训练。未启动游戏、本地构建或高负载测试；验证交给远端质量门禁。

## 历史记录 · 2026-10-15 Money 生产包单次深验编排

- `Update-MoneyThresholdNativeOutcomeBatch.ps1` 现通过一次 `build-money-threshold-production-package` 调用生成 batch、canonical corpus、18-cell work plan 与可选 coverage request，不再启动三个进程并在同一轮内重复深验同一 corpus。
- 原生 run、comparison admission 与 trajectory 仍在 batch corpus 构建时完整重放一次；后续两步只能复用该进程内已经验证的对象，并必须同时匹配刚落盘 corpus 的绝对路径、文件 SHA-256 和行级不变量。独立命令与下游 coverage/reconciliation 仍在各自信任边界完整重放，未引入跨运行缓存，也未弱化 fail-closed 规则。
- 这项优化服务 #267 的 proof DAG 单次计算与 #274 的测量优先原则：它消除代码上可确认的重复调用，但没有猜测缓存预算、并发度或内存淘汰参数。没有启动本地游戏或高负载测试，Money production 状态仍为 `0/18`，正式训练仍为 `false`。

## 历史记录 · 2026-10-15 Money 完整原生轨迹与 corpus v4

- 新增 `money_threshold_native_trajectory_receipt.v2`。它从权威 Revenue request denominator 中选择一个待验 request，重建完整 terminal proof，并复用既有单候选 verifier 连接可选 acquisition/processing rollout、终端 sale/shipping 动作与 Revenue recurrence；不新增第二套执行器、收益验证或比较器。
- trajectory receipt 保存 source/final checkpoint、最终快照、concrete alternative、完整 Revenue prefix、terminal evidence SHA 与传递 artifact 哈希，固定 `teacher_preference_label_verified=false`、`formal_product_training_authorized=false`。manifest 中的 request ID 只指定验哪条原生轨迹，不构成偏好标签。
- Money corpus/request 升级为 v4，同一 canonical builder 现在接受隔离 sale episode 或完整 trajectory。comparison join 对 trajectory 强制匹配 selected concrete alternative、acquisition 标志、terminal evidence、完整 prefix 与 terminal artifacts，禁止把 rebound 或 shipping 截断成最后一次出售/结算。
- batch request v4 与 comparison index v3 可携带一个 comparison-selected trajectory；完成脚本会先构建并独立重放 trajectory，再建立 criterion-local join。若同一 seed 跨越的 criterion 选择不同 alternative，仍要求不同 seed run，不能把多个原生结果塞进一行。
- 单候选验证现在把已经验证的 source checkpoint 与 final snapshot 通过内部上下文交给 trajectory builder，避免重复读取同一 source/recurrence；同一次完成编排中，多个 criterion 选择同一 alternative 时也只构建一次 trajectory receipt，再分别执行 criterion-local join。
- 新增 `money_threshold_coverage_request_manifest.v1` 与 `build-money-threshold-coverage-request`。它先重放 Money corpus，再从现有 coverage request 中移除全部旧 Money source、保留其他 source，并加入恰好一个新 Money source；与非 Money source ID 冲突时失败关闭。batch updater 仅在显式传入 `-CoverageRequestTemplate` 时生成该请求，默认不会触碰 production coverage。
- 这些变更关闭表示与编排代码缺口，但没有采集 production evidence；Money 仍为 `0/18`，正式训练仍为 `false`。


## 历史记录 · 2026-10-14 Strategy-ledger 边界制品身份

- 新增 `build-strategy-ledger-boundary-capture` 与 `strategy_ledger_boundary_capture.v1`。每份制品以内容寻址 ID 绑定 `before_terminal_action` 或 `after_terminal_action`、request、concrete alternative、原生执行 `run_id`、快照 SHA/state/tick，以及账本 SHA/ID/revision/source-state/actor；调用方不能再只提交两条裸账本路径。
- Money Revenue terminal manifest/receipt 升级为 v2。证明器会从制品引用的真实快照和账本重新构造完整 capture 并逐 JSON 核对，同时要求 before/after 是不同文件、不同 capture ID、相同执行 run；before 仍必须等于 request 锚点，after 仍必须是独立账本文件且字节和 SHA 不变。
- hermetic 回归覆盖正常出售、前后 capture 互换拒绝、账本改写后旧 capture 失效及全量 reservation 拒绝；Bootstrap Release 构建 `0 warning / 0 error`。未启动游戏或外部测试机，`formal_product_training_authorized=false` 保持不变。
- 这关闭 #236 的外部 strategy-ledger before/after artifact identity 代码缺口。该 issue 只剩代表性真实快照的 wall/CPU 测量与证据索引验收，不能用 hermetic 计时替代。

## 历史记录 · 2026-10-14 Planner / Executor 路径与终端计时统一

- `RouteGridPathfinder` 现在是当前地图候选、执行器到达校验和未来日期距离场的共享 BFS 原语。当前候选按 `SnapshotEnvelope + StateHash` 缓存一份整数距离场；同一快照重复评估不会按候选重扫全图，执行器单目标校验仍采用命中即停止的 BFS。
- Full Shipment 当前态采集链已从 Manhattan 估算切换为同一 BFS 语义，覆盖动物产物、机器产出、地面采集、姜、灌木、果树、春葱、树上产物、树苔、野树砍伐、鱼塘、晶球、远古斑点及社区中心奖励。不可达站位在候选上游失败关闭，不再等到执行器阻塞。
- `AcquisitionCurrentRouteCandidate` 统一保留 `route_distance_tiles`、`terminal_action_ticks` 与计时投影模式。日预算只计算一次“到达精确站位 + 终端动作”；野树砍伐与放射性矿石/怪物目标也已接入该入口，移除了各自手写的路程扣减或当前时间直算分支。
- 定向回归证明绕墙场景的候选与执行器均采用 5 格 BFS 距离，且同一快照两次候选评估只构建 1 次距离场。Bootstrap Release 构建为 `0 warning / 0 error`，Core game-free 回归 `346/346`，acquisition dispatch 与 bootstrap hermetic 自测通过；未启动游戏或外部测试机。
- 本切片关闭 #236 的共享 route/arrival/terminal timing 代码缺口，但不据此授权正式训练；`formal_product_training_authorized=false` 保持不变。后续外部 strategy-ledger 边界身份已由上一节补齐；#236 仍需在代表性真实快照上补一次 wall/CPU 测量后再关闭性能验收项。

## 历史记录 · 2026-10-14 Money evidence work plan

- 新增 `build-money-threshold-evidence-work-plan`，从权威六档 Money criterion 与固定 train/validation/test 分区生成 `6 x 3 = 18` 个不可增删的工作格。输入可以不带 corpus（全部待采）或带一份可重放验证的 Money corpus v3。
- 每格分别统计 native outcome 与 exact-denominator Teacher comparison，不用 method 汇总掩盖 criterion/partition 缺口。输出保留命中的 row ID，并只给出三种状态：完整、已有 outcome 但缺 comparison、两通道都缺。
- work plan 只负责调度缺失证据，不能生成 episode、提升 fixture 或授权训练。当时本机 E 盘 Money 目录只有 self-test；hermetic 示例因此是 `native 1/18`、`comparison 0/18`、`complete 0/18`，正式覆盖仍不变。

## 历史记录 · 2026-10-14 Money criterion comparison join

- `money_threshold_teacher_corpus_request/corpus` 已升级为 v3。它仍以原生 ShopMenu sale episode 为 native outcome 唯一来源，但现在允许每个 crossed criterion 显式附带一份 Revenue comparison admission；没有 admission 的行继续只计 native outcome。
- `money_revenue_comparison_admission_receipt.v2` 在既有 exact-ID 分母和严格 Pareto 结果之外，保留已验真的 native terminal artifact 列表。Money join 会重新构建 manifest/receipt，并要求 criterion、阈值、唯一优选、完整 rejected 集合、save/player、首条 Revenue transition，以及 sale episode、before/after snapshot、execution result 的路径和 SHA-256 全部一致。
- Goal Method adapter 只为完成上述 join 的 criterion/partition 增加 Teacher comparison coverage；原生成功本身仍不能自我标注。hermetic 正例绑定 49,990 -> 50,010 的优选 sale，删除选中 episode artifact 的负例会失败关闭。
- 该切片打通了真实比较凭据进入 Money criterion view 的代码路径，但没有制造 production 三分区数据；正式状态仍为 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Money threshold native-outcome isolation

- `money_threshold_teacher_corpus_request/corpus` 升级为 v2。raw corpus 现在只证明原生 ShopMenu sale 的 before/after、收益增量与权威阈值跨越，不再把“已经执行的出售”反推成 Teacher preferred action。
- row 与 criterion 的 `teacher_comparison_verified` 固定为 `false`，preferred/rejected 字段固定为空；`native_terminal_outcome_verified` 保持真实。任何 raw corpus 混入 Teacher 声明都会在构建、重放或 coverage adapter 处失败关闭。
- Goal Method coverage 的 raw Money adapter 现在只计入 native outcome partition，Teacher comparison partition 保持为空。当时后续计划必须用上一切片的 exact-ID comparison admission 与对应原生 episode 显式 join，才能恢复 criterion-local comparison coverage。
- 新增轻量 hermetic 阈值跨越回归：49,990 -> 50,010 的原生 sale 只产生 `money_50000` native outcome，不产生偏好。正式状态与 `2/19`、`1/11`、`formal_product_training_authorized=false` 均不变。

## 历史记录 · 2026-10-14 Revenue exact-ID comparison admission

- 新增 `build-money-revenue-comparison-admission`、`money_revenue_comparison_admission_manifest.v1` 与 receipt。
  admission 会重建 terminal execution proof 与 counterfactual request catalog，要求 request、terminal proof、terminal evidence 三组 ID 完全相等，并按 request 顺序推导 concrete alternative ID；调用方不能再自报比较分母。
- direct alternative 只消费已验证 terminal opportunity cost；rebound alternative 复用既有 comparison artifact verifier 重建 acquisition rollout、reservation 与 acquisition opportunity cost，随后和 terminal cost 聚合。没有新增第二套 rollout、Revenue recurrence 或 Pareto 比较实现。
- 每条 recurrence 必须连接对应 terminal action，并保持 source save/player/total-money identity。完整分母进入现有 reserve-aware strict-Pareto comparer；只有唯一前沿候选时，receipt 才设置 `production_teacher_preference_label_eligible=true`，不可比较前沿保持无标签。
- hermetic 用同一 source state 构造两条收益相同、真实终端耗时分别为 1/2 游戏分钟的直接出售证明；较快候选被唯一选中，删除任一 evidence 会失败关闭。Release 构建 `0 warning / 0 error`，未启动游戏或外部测试机。
- 该层关闭 Revenue 当时 concrete denominator 的比较准入，但不冒充 criterion 三分区原生 outcome admission。下一固定切片是让 Money method-family 的独立 train/validation/test criterion view 消费该 preference 与原生结果；正式状态仍为 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue terminal execution proof

- 新增 `build-money-revenue-terminal-execution-proof`、
  `money_revenue_terminal_execution_proof_manifest.v2` 与 receipt。manifest 必须对 authoritative request catalog
  中每个 request ID 恰好提供一份证明，漏项、重复项和额外项均失败关闭；request catalog 会从源 manifest 重建并逐 JSON 核对。
- 每份证明从 request 固定的 terminal snapshot 与 terminal strategy-ledger anchor 开始。可选 route transition 只能复用现有
  queue/receipt validator 和资源中性移动、连接器、交互、菜单及等待动作；版本化 persistent boundary 必须证明路由期间库存、货币、精力和持久状态未变。
- 终端动作复用现有原生 shop-sale episode 或 shipping-deposit verifier，并强制接入现有 Revenue recurrence receipt。
  商店出售额外核对透明 material graph 的精确槽位、物品、品质、单价与数量减少；出货仍要求精确入箱队列并以次日原生结算作为 recurrence 首跳。
- before/after 必须各自提供 `strategy_ledger_boundary_capture.v1`，绑定边界阶段、request、候选、原生执行 run、快照与账本内容身份。before ledger 必须就是 request 推导出的权威终端账本；after ledger 必须是独立路径但字节和 SHA 完全相同。
  当时玩家库存槽位通过共享 `MaterialSupplyProjection` 与全部 active reservation 联合投影，只有未预留数量足以覆盖出售/入箱数量时才准入。
- 终端总耗时取真实 receipt 的起止 tick，并统一通过 `GameClockBudgetPolicy.TicksToGameMinutes` 转换；没有复制 BFS、商店、出货、账本或执行器实现。
  Release 构建为 `0 warning / 0 error`，轻量 hermetic 覆盖未预留商店出售正例和全量预留拒绝，未启动游戏或外部测试机。
- 本层得到的是完整 terminal proof denominator，不是生产偏好。下一固定切片是以这些 evidence 的 request ID 为唯一分母，构建严格 comparison admission，随后才可判断
  `earn_money` 是否获得 production Teacher preference。正式状态保持 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue counterfactual proof requests

- 新增 `build-money-revenue-counterfactual-proof-requests`、
  `money_revenue_counterfactual_proof_request_manifest.v1` 与 catalog。它重建 concrete alternative catalog，绑定同一
  source snapshot 的 strategy ledger，并为每个 concrete ID 恰好生成一份不可由调用方增删的采证请求。
- direct 候选明确不要求也不允许伪造 acquisition rollout；rebound 候选保留已验证 rollout manifest/receipt。
  sale 请求要求原生 shop-sale proof，shipping 请求同时要求原生 deposit 与次日 settlement；两类都要求完整 Revenue
  recurrence。
- 每个请求现已携带不可由调用方填写的 terminal strategy-ledger 锚点。direct 候选必须逐字段沿用 source ledger；
  rebound 候选会重建 acquisition rollout，并从最后一笔已验证 settlement 反查 ledger path/hash/ID/revision/source state。
  终端账本与 rollout receipt 或 terminal snapshot 任一漂移都会失败关闭。
- 审查中确认现有 comparison 只聚合 acquisition opportunity cost，不能把最终路由、出售或入箱动作默认为零成本。
  外部 strategy ledger 的保留项也不能只靠游戏内保护标记推断。因此 8/8 请求统一强制
  `terminal_opportunity_cost_proof_required=true` 与 `terminal_reservation_claim_proof_required=true`。在共享时序/成本和
  储备安全证明接入前，已有 comparison 只能保持非生产证据，不能提升 Teacher 标签。
- hermetic 覆盖 `8 concrete -> 8 proof requests`、`3` 份 rebound 保留 rollout、`5` 份 direct 不伪造 rollout、
  `5` 条 sale 与 `3` 条 shipping 证明形态，以及陈旧 ledger 拒绝。Release 为 `0 warning / 0 error`，未启动游戏。
- request denominator、采证调度与终端账本起点已权威，但执行结果和账本前后连续性证明仍为空；正式状态继续为 `2/19`、根方法 `1/11`、
  `formal_product_training_authorized=false`。下一固定切片是实现共享 terminal opportunity-cost 与 reservation proof，
  再让每个请求绑定 native recurrence receipt，最后使 comparison receipt 的 ID 集与 request catalog 完全相等。

## 历史记录 · 2026-10-14 Revenue concrete alternative 联合分母

- 新增 `build-money-revenue-concrete-alternative-denominator`、
  `money_revenue_concrete_alternative_denominator_manifest.v1` 与 catalog。构建器会重新构建路径骨架、terminal rebind
  receipt 及其全部叶子证据，再将 direct sale/shipping 骨架和 acquisition/processing 后的 rebound 候选合并为一个
  不由调用方声明 ID 的具体候选分母。
- direct 候选严格复用源 candidate 身份和源 state；rebound 候选严格复用已验证的新槽位、terminal state、rollout
  ID/manifest/receipt 与 terminal candidate。两类候选进入同一 schema，但保留不同的 acquisition、shop-sale、shipping
  deposit 和 shipping settlement 证明要求，不会用空 rollout 冒充直接销售，也不会把直接出货当成已隔夜结算。
- hermetic fixture 中 7 个路径骨架稳定展开为 `5 direct + 3 rebound = 8 concrete`；同一新物品的两个商店路径均保留，
  删除任一 rebound 骨架证据会失败关闭。Release 构建为 `0 warning / 0 error`，快速密封回归通过，未启动游戏。
- 该 catalog 现在可以声明 `current_concrete_denominator_authoritative=true`，含义仅限当前状态下的候选身份完整。
  尚未为每个 concrete ID 生成隔离 counterfactual 收益 recurrence，因此 production label eligibility 与
  `formal_product_training_authorized` 仍为 `false`，正式状态仍是 `2/19`、根方法 `1/11`。
- 下一固定切片：由 concrete catalog 生成逐项 proof request，并使 Revenue comparison proof 精确消费该目录 ID；
  direct 候选走原生 sale 或 deposit+settlement，rebound 候选先验 acquisition rollout 再走相同终端证据，不再接受
  comparison manifest 自报候选全集。

## 历史记录 · 2026-10-14 Revenue fresh terminal 候选重绑定

- 新增 `build-money-revenue-terminal-candidate-rebind`、
  `money_revenue_terminal_candidate_rebind_manifest.v1` 与对应 receipt。manifest 必须对路径骨架中每个
  `requires_fresh_terminal_candidate_rebind` 项恰好提供一份完整 acquisition rollout proof；漏项、重复项和额外项
  均失败关闭。
- 构建器会重建 alternative denominator、完整 rollout 与持久化 receipt，再按骨架的原始 `source_candidate_id`
  自动定位唯一已执行 transition。调用方不能声明产物、槽位、价格或终端候选；这些字段只从已验证 fresh receipt、
  前后透明快照和现有 `MoneyRevenueCandidateDenominatorBuilder` 得出。
- 库存绑定按精确品质和槽位计算。旧库存中的同类物品不会被误算为本次产物；若同一新增槽位可由多个商店收购，
  所有不同商店/价格/路线候选都会保留为不同 concrete alternative，而不是任意只取一个。sale 与 shipping 继续使用
  原候选生成器，没有新增第二套售价、保护、BFS、编译器或执行器逻辑。
- Release 构建为 `0 warning / 0 error`，快速 hermetic 已覆盖旧槽排除、一个新槽展开两个商店候选、同槽出货以及
  receipt 净增量漂移拒绝；未启动游戏，也未运行高成本完整 Stage-One fixture。
- 该 receipt 只证明“所有待重绑定骨架均已得到可重放的具体终端候选”，尚未把直接终端骨架与 rebound 结果合并成
  最终 concrete alternative denominator。因此正式状态保持 `2/19`、根方法 `1/11`、
  `formal_product_training_authorized=false`。下一固定切片是构建 direct + rebound 的唯一联合目录，并让 comparison
  proof 从该目录生成候选集合，而不再接受调用方自报 alternatives。

## 历史记录 · 2026-10-14 Revenue 原生出货入箱证据

- 新增唯一 `NativeShippingDepositEvidenceVerifier`，把 `executor.ship_inventory_item_to_bin` 的末队列项、fresh
  回执、精确库存槽、物品/品质/数量/实时单价、Farm 箱端点、待结算金额、现金、累计收入和日期绑定为同一证据。
- 修复共享钱包多物理出货箱重复展示同一内容时的重复计数风险：正式证据只读队列绑定端点；旧 Stage-One
  兼容读取要求同 scope 端点完全一致并只计一次。
- 密封测试覆盖双共享端点正例，以及错槽位、错单价、错端点、入箱即到账、changed-facts 与快照不一致五类负例；
  Bootstrap Release 为 0 warning / 0 error，关键密封路径通过，未启动游戏。
- 状态不升级：该切片只闭合入箱，隔夜结算复用既有共享验证器；正式覆盖仍为 `2/19`、根方法 `1/11`，
  `formal_product_training_authorized=false`。
- 下一固定切片：对每个 acquisition/processing rollout 的 fresh terminal snapshot 重建 Revenue 候选目录，将实际
  产物唯一重绑定到路径骨架要求的 `economy.sell_items` 或 `economy.ship_items` 终端，再生成具体 alternative 分母。

## 历史记录 · 2026-10-14 Revenue 候选到结算路径骨架

- 新增 `build-money-revenue-alternative-denominator` 与 `money_revenue_alternative_denominator_catalog.v1`。它先重建并逐 JSON 核对当前候选目录，再沿权威 `earn_money` 依赖图把每个 policy candidate 展开到唯一累计收益里程碑；只枚举简单路径，因此不会进入 recurrence 回边形成无限展开。
- 获取/加工候选会自然分叉为 shop-sale 与 shipping 两条路径，机器容量会自然经过 load/collect；直接 sell/ship 候选只保留对应终端。路径、edge kind、源候选 SHA 与 proof shape 全部进入确定性 alternative ID，没有手写第二套行为路由表。
- 该层只证明 `alternative_skeleton_denominator_complete`。直接出售/出货的具体终端身份已知；采集、加工、钓鱼等动作必须在结果快照上重新绑定实际可卖/可出货候选，尤其不能把随机鱼种提前当成已知结果。因此当前 fixture 正确输出 `current_alternative_denominator_authoritative=false`，并记录 fresh terminal rebind 数量。
- Release 构建与快速 hermetic 通过；覆盖采集候选的双结算分叉、直接 sell/ship 证明形态、断边失败关闭。真实 13 节点图烟测正确识别 `settled_milestone_ladder`；临时快照透明字段不全时仍失败关闭。下一块是 fresh terminal rebind 与 shipping deposit 原生 proof，而不是把原子候选直接改名成 rollout。

## 历史记录 · 2026-10-14 Revenue 当时快照权威候选分母

- 新增 `build-money-revenue-candidate-denominator`；当前 schema 为 `money_revenue_candidate_denominator_catalog.v2`。构建器从 `goal_method_dependency_expansions.v1` 的 `earn_money` 节点自动发现全部带 `option_id` 的 policy transition 与确定性 support transition，再对同一透明快照只调用一次现有 `CandidateOptionAvailabilityEvaluator`；没有复制售价、保护、路径、编译器或执行器规则。v2 同时保留服务时间窗、allowed-now/allowed-today、等待成本、gate 原因、库存/货币及 Full Shipment 相关候选语义，防止采证调度与反事实比较漏字段。
- 目录同时保留 event/economic 两类候选、滚动 continuation、参数、阻塞原因和逐候选内容哈希。节点漏评、重复候选 ID、缺失透明字段、候选 kind 既不直接匹配依赖图也不绑定 continuation 时均失败关闭；阻塞候选不会被静默删除。
- 权限被拆成三层：`current_snapshot_denominator_authoritative` 只说明当前快照的依赖节点和候选枚举不重不漏；`current_comparison_denominator_ready` 还要求至少两个当前可比较候选；`long_horizon_denominator_authoritative` 继续受 fresh-save schedule、counterfactual opportunity cost 和 crop route 三个现有 blocker 约束。目录不含 rollout proof，因此 production Teacher label 与正式训练授权始终为 `false`。
- Release 构建 `0 warning / 0 error`，快速 `self-test-bootstrap-hermetic` 通过；测试覆盖直接候选、跨图 staged continuation、漏 option 与未绑定 kind。未启动游戏或运行高成本 Stage-One fixture。该目录是替代方案路径展开的源分母，不能跳过动作后 fresh terminal rebind 直接绑定整条 rollout。

## 历史记录 · 2026-10-14 Revenue 候选比较可重放工件

- 新增 `build-money-revenue-alternative-comparison-proof`、`money_revenue_alternative_comparison_proof_manifest.v1` 与对应 receipt。每个候选不再由调用方直接拼接 prefix/cost/claim，而是必须引用完整 acquisition rollout manifest+receipt 和 Revenue recurrence manifest+receipt；两张 receipt 都必须逐项等于控制器重算结果。
- acquisition rollout 的每个已执行 transition 都重新绑定唯一 route occurrence、确定性 opportunity-cost report、该 route 的 reservation claim set 与已验原子 portfolio commit。所有 transition 的非标量成本由共享 `AcquisitionOpportunityCostVectorAggregator` 聚合，原 portfolio admission 同步复用该实现，没有出现第二套成本算法。
- 比较候选必须共享完全相同的起始 state hash、snapshot SHA-256、strategy-ledger SHA-256 与 ledger revision；rollout 最终 snapshot 的完整 Money checkpoint 必须等于 Revenue prefix 的第一 checkpoint，且 acquisition 过程不得暗中增加累计收入。篡改持久化 Revenue receipt 会失败关闭。
- 当时 manifest 仍是调用方声明的候选集合，尚无权威 Revenue alternative enumerator 证明“所有可行赚钱方案均已列入”。因此 receipt 明确输出 `candidate_denominator_authoritative=false`、`production_teacher_preference_label_eligible=false`、`formal_product_training_authorized=false`；正式状态仍为 promotion-grade `2/19`、root method `1/11`。下一块固定为从透明快照与权威 acquisition/processing route 生成 Revenue 候选全集，再让本工件消费该权威 denominator。
- 本机只执行低成本验证：GoalConditionedBootstrap Release `0 warning / 0 error`，`self-test-bootstrap-hermetic` 退出码 `0`。完整 Stage-One fixture 历史开销约 30 分钟、183 GiB 读取，本轮没有在本机重跑；新增成功链回归已挂接在该完整 fixture，但轻量 PR CI 不执行这一高成本项，因此本轮不能宣称完整工件成功链已动态通过。未启动游戏。

## 历史记录 · 2026-10-14 Recovery -> settlement 持久状态边界

- 经核对，远端最新 issue 仍为长期总纲 `#236`，没有更新编号。其 action-origin debris 与 acquisition output sufficiency 两项已由共享实现覆盖；recovery 只比较 Full Shipment 进度、日期和 shipping bin 的意见仍然成立。
- 新增版本化 `full_shipment_recovery_settlement_boundary.v1` 投影。连接器移动、菜单、动画和场景候选上下文是显式瞬态白名单；玩家背包、四类原生货币视图、累计收入、日期、shipping bin、world progress、quests 及其余未声明字段进入稳定指纹，未知字段默认按持久字段处理。
- `FullShipmentRecurrenceProofBuilder` 现在同时验证 prior -> before 边界连续性以及 before -> after 的背包、货币和总持久状态不变性。合法移动继续通过，注入 Money 或 inventory 漂移会失败关闭。GoalConditionedBootstrap Release 构建 `0 warning / 0 error`，聚焦 `self-test-full-shipment-settlement` 通过；未启动游戏或全量回归。
- 该切片修复快照内持久状态漏洞，但不宣称关闭整个 `#236`：controller 外部 strategy ledger 的 before/after 工件身份、统一 route/timing authority 与实测 profiling 仍是后续项。正式状态保持 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue recurrence 可重放工件

- 新增 `build-money-revenue-recurrence-proof` 与 `MoneyRevenueRecurrenceProofBuilder`。manifest 的每条边只能选择 `native_shop_sale` 或 `native_shipping_settlement` 一种证明形态；前者重新验真 shop execution episode，后者重新验真 sleep queue、before/after snapshot 与 queue receipt，混填或缺字段直接拒绝。
- receipt 保存 manifest 哈希、每条边四个源工件的绝对路径与 SHA-256、重新投影的 Money progression 以及最终连续前缀。hermetic 已把 20g 即时销售与 300g 隔夜结算组合为 320g 两边工件，并证明相邻 checkpoint 连续；伪造 selected candidate 无法通过。
- 这关闭了“内存 prefix 无法重放”和“各边真实但拼接时偷换状态”两个缺口。它仍是合成 fixture，不增加 production coverage；下一块让 Revenue comparison manifest 引用该 receipt、确定性 opportunity report、strategy ledger/commit 与隔离 counterfactual rollout。

## 历史记录 · 2026-10-14 Revenue 储备感知候选比较

- 新增 `MoneyRevenueAlternativeComparer`，只接受来自权威 `GrandpaEvaluationGoalEvaluator.MoneyThresholds` 的 criterion、完整且不重不漏的候选 ID 分母、同一收益前缀起点及同一 reservation ledger revision。调用方不能自报任意收益目标。
- 每个候选重新验证 Revenue 前缀，并要求 acquisition 既有的非标量时间/能量/材料/货币成本向量与原子 reservation claim set 数量一致；陈旧状态、替换他人储备、claim/成本漂移均 fail closed。目标阈值满足是第一层权威终态，同一终态层内仅在收益进度、完成日和储备感知成本上存在唯一严格优势时才生成偏好；不可比候选保留在前沿，不读取 learner score。
- hermetic 覆盖唯一达标候选、收益/木材/时间 trade-off、候选漏项、陈旧 claim 和数量漂移。Revenue prefix 现已有独立可重放 receipt，但 comparison 尚未绑定真实 acquisition route、ledger commit 与隔离 counterfactual rollout，因此 `formal_product_training_authorized=false` 和正式覆盖率不变。

## 历史记录 · 2026-10-14 Revenue 连续前缀

- 新增 `MoneyRevenueRecurrenceVerifier`，把已验真的即时商店销售与原生隔夜发货结算编排为同一 save/player 的类型化收益前缀。相邻边必须完整复用上一边的 `MoneyRevenueCheckpoint`，因此状态哈希、日数、现金、累计收入和 shipping scope 不能在边界间静默漂移。
- 验证器不会信任调用方构造的公开 record：每条边都会重新核对 kind、正收益 delta、日推进、pending shipping 消耗、Year 3 截止和聚合总额。hermetic 已覆盖 320g 双类型前缀，并拒绝断链哈希、伪造 delta 与截止日外边。
- 该类型证明“收益边可以连续组合”，但尚未证明从新存档取得这些边。后续 reserve-aware comparison 已接入现有非标量成本与 reservation claim 语义，但真实 acquisition/processing 支持转移和 counterfactual 工件仍待绑定。正式状态仍为 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 通用原生 shipping-settlement evidence

- 单项原生 sleep queue、queue execution receipt 与隔夜 Money progression 现由 `NativeShippingSettlementEvidenceVerifier` 联合验真；`executor.sleep` 和带 `execution_option_id=executor.sleep` 的 `recovery.stabilize_day` 继续复用同一执行器。
- Full Shipment 普通与终局 settlement receipt 已改为消费该共享入口，不再分别只校验“睡了一天”和 collection 变化而遗漏实际金额归因。合法发货收益必须与前夜去重后的 pending shipping value 精确一致。
- hermetic 同时覆盖有效 sleep receipt 与非 sleep queue 拒绝。历史工件若缺少 Money 或完整 shipping-scope 字段将 fail closed 并需重采；不会降低或伪造现有 production 分子。
- 即时 sale 与隔夜 shipping 两种收益边至此都有唯一原生验真入口。当时后续计划把它们编排成同 actor、状态哈希连续、截止 Year 3 的 Revenue recurrence prefix，并接入 reserve-aware alternative 比较。

## 历史记录 · 2026-10-14 通用原生 shop-sale evidence

- 原先位于 Money 阈值 corpus 内部的 execution episode/header/hash/queue/result 验真已下沉为唯一 `NativeShopSaleEpisodeVerifier`。阈值 corpus 现在只负责在通用 sale progression 上派生 crossed criterion，不再复制执行证据验证。
- 通用证据保留 `shop_id`、`qualified_item_id`、inventory slot、quantity、unit price、total value、queue item、before/after/result 哈希及 Money progression；结构化 command 中任一字段缺失、重复、冲突或总价不等于实际收入增量都会 fail closed。
- 该入口不要求一次销售必须跨 Grandpa 阈值，因此后续 Revenue recurrence 能记录全部有效销售边，而阈值 Teacher coverage 仍只接收真实跨线视图。现有伪造 quantity 回归仍通过，正式覆盖率不变。

## 历史记录 · 2026-10-14 Money progression 共享验真基础

- `MoneyRevenueProgressionVerifier` 统一投影 `save/player/day/money/totalMoneyEarned` 与 shipping settlement scope，现有 Money 阈值 sale corpus 已改为复用该入口，不再保留第二套金额/天数/actor 读取逻辑。
- 商店即时结算必须满足当前现金与累计收入同额正增长、同日、同 actor，且 pending shipping value/signature 完全不变；这阻止一次销售证据混入隔夜发货收益。
- 原生发货结算必须恰好跨一天，现金与累计收入增量都严格等于前夜 pending 值，结算后 pending 为零。多个物理 shipping-bin endpoint 若属于同一 shared/personal scope 只计一次，并且所有副本必须一致，避免重复端点把收益翻倍。
- hermetic 已覆盖即时销售、发货结算、同 scope 三端点去重、pending 漂移拒绝和重复计价拒绝。该层只是 `earn_money` recurrence 的共享原生状态验真基础，不会单独提高 production coverage；当时后续计划仍是把 acquisition/reserve/processing/sale/shipping/settlement 组合成连续的 fresh-save recurrence。

## 历史记录 · 2026-10-14 Money 阈值族判据级证据边界

- Teacher coverage 不再假设“共享同一 root method 的 criterion 必须拥有完全相同证据”。旧实现会让任意一个 Money 阈值样本污染全部六档；现改为 method 级共享 progression 加 criterion 级独立证据，方法汇总只取所有所属 criterion 的共同三分区覆盖。
- 新增 `money_threshold_progression_corpus` 强类型入口。请求只能提交原生执行 episode，不能提交 criterion ID、阈值或分区标签；构建器只接受 `executor.sell_shop_item` 的 applied/verified 原生回执、fresh 前后快照、完整哈希绑定，以及 `player.money` 与 `player.total_money_earned` 完全相同的正增量。
- 六个阈值现在由 `GrandpaEvaluationGoalEvaluator.MoneyThresholds` 单点定义。一个真实 progression 可以派生多个 crossed-threshold view，但每个 view 独立保存 Teacher goal context、终态断言、comparison receipt 和 train/validation/test 覆盖，不允许“高阈值通过即自动放行低阈值”。
- 隔离自测位于 `E:\StardewAI-TestLab\artifacts\goal-method-money-threshold-selftest-20261014`：三份独立 save/player/day 样本只跨越 50,000，报告证明仅 `money_50000` 获得三分区证据，其余五档仍为空；伪造 criterion 归属会在重放时失败关闭。
- 该自测是结构与验真测试，不是 production native evidence。正式状态仍为 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。下一施工单位是闭合 `earn_money` 的 fresh-save 收益 recurrence 与 opportunity-cost Teacher 依赖，再采集生产三分区证据。

## 历史记录 · 2026-10-14 正式训练准入重算与下一施工单位

- 已使用当前 frontier 输入和当前代码重新生成三分区 acquisition 语料、checkpoint 与支持转移终端谱系，并完成只读 `build-goal-method-formal-training-admission-reconciliation`。报告位于 `E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation.json`；旧 2026-09-28 冻结语料因日历解析漂移已被拒绝，不能继续代表当前状态。
- 当时机器结论为 `2/19` promotion-grade criterion、`1/11` coverage-ready root method；语料为 `3` 个来源、`9` 行，train/validation/test 各 `3` 行、各 `2` 个 pair，重算 validation/test accuracy 均为 `1.0`。支持转移终端谱系已从旧记录的 `5/5` 更新为 `6/6`。
- 完整性、语料重放、三分区、checkpoint 绑定、holdout 重算、支持谱系与叶制品权限隔离均通过。Teacher 仍阻塞于 `dependency_graph_incomplete:15` 与 `evidence_not_connected:2`。全局 option 对账确认三个清障 primitive 原本只是未在 capability source 绑定既有 `EVD-112/189/212`；绑定后相关透明读取、原生运行和 five-gate 告警全部消失，且三者仍为 `EvaluationOnly`、不进入策略训练 allowlist。
- 三个社交高层 option 现已显式登记到唯一 `DailyPlanCompiler -> executor.social_interact` 管线：候选种类白名单覆盖跨图 connector、菜单恢复、有限重试与 talk/gift/partnership 终端，能力源同步声明 step+parameter compiler 和 internal execution。直接高层 action 仍失败关闭，求婚/室友等确认规则不变。新 matrix/lowering 重算后 `referenced_option_execution_inventory=true`、`runtime_product_promotion_ready=true`，下游 promotion blocker 已为零；正式报告为 `E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation-social-pipeline.json`。
- 训练授权继续保持 `formal_product_training_authorized=false`。下一施工单位按 #224 的 method-family 顺序进入 Money 单调阈值族：复用一个 progression、候选分母、Teacher source adapter 与三分区管线，为六个 money criterion 生成独立目标上下文和终态视图；不得按六个 criterion 复制六套实现。
- Goal-method corpus 重放失败现在会报告精确 source index、`rollout_id` 与 proof manifest 路径，同时继续失败关闭；这用于快速定位漂移，不放宽 digest、回放或授权规则。

## 历史记录 · 2026-10-14 Full Shipment 运行时证据分母闭合

- Full Shipment 权威清单保持 `154/154` 个需求、`641` 条来源、`26` 个执行签名层；严格 production evidence index 已达到 `26/26`，共享 shipping/settlement 证明有效，缺失层为 `0`。索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v38-20261014`。
- 最后一层 `tree_moss_harvest_sample / full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest` 已闭合。唯一动作链为 `foraging.harvest_tree_moss -> executor.clear_obstacle -> executor.pickup_debris`：镰刀仍由原生工具周期执行，落地物继续复用共享自然拾取执行器，没有新增第二套采收或拾取系统。
- 正式树苔 proof 位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-tree-moss-harvest-v7-20261014`。队列精确包含移动、一次清障和一次拾取；取得 `(O)Moss x1`，冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 重建历史层时发现旧 `machine_item_query_output_sample` 已被当前路线身份规则淘汰。处理层现在把“同物品但具有完整、不同权威来源”的当前机器产出判为确定性 route miss；只有来源缺失或不完整时才以 `machine_active_output_source_unresolved` 失败关闭。新的 v3 proof 位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-item-query-output-v3-20261014`。
- `formal_product_training_authorized=false` 仍是正确结果。`26/26` 只闭合 Full Shipment 内部运行时样本分母；当时后续固定计划是运行并修正现有 `build-goal-method-formal-training-admission-reconciliation`，从当时 19 项爷爷评分目标、语料、三分区、checkpoint、支持转移和所引用执行器生成真实阻塞清单。任何叶子证据均不得自行授权训练。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：共享机器产出族闭环

- `machine_output_sample`、`machine_flavored_output_sample`、`machine_item_query_output_sample` 与 `solar_panel_output_sample` 已从冻结源存档的隔离副本完成原生采证。四层都复用唯一的 `farm.collect_machine_outputs -> executor.collect_machine_output` 链，没有复制候选、编译器或执行器。
- 当前机器候选以完整 `route_kind + source_id + qualified_item_id` 保留权威来源身份；同一已物化产出存在多个等价权威描述时，只让字典序 canonical 来源进入 Teacher 前沿，透明桥仍保留全部来源。数量与品质统一通过 `AcquisitionOutputProof.CanSatisfy` 验证，缺失证明继续失败关闭。索引缓存 schema 升至 `v13`。
- 太阳能板不再依赖静态占位：只有透明快照存在精确、可收取的当前太阳能板产出时，才进入日历、位置、设施、资源、加工、随机性、日内预算与 dispatch 轴；旧快照或来源漂移不会生成候选。
- 原生收取验证器现在区分“产物清空”和“原生立即排入下一生产周期”。后一种成功必须同时满足交互成功、目标物品实际入包、机器退出可收获态、下一周期计时为正且 held object 已更换；树液采集器仍额外要求同格原生已安装树液采集器树。蜂房回放据此不再被误判为失败。
- 四个严格 proof 根依次为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-output-v5-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-flavored-output-v2-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-item-query-output-v1-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-solar-panel-output-v1-20261014`。冻结源存档 SHA-256 均保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已集中深验到 `25/26`；唯一剩余层为 `full_shipment.runtime_sample.21.native_tree_moss_harvest`，`formal_product_training_authorized=false`。索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v36-20261014`。Core `2680/2680`、Backend `229/229`，Bootstrap 与 RuntimeTestHarness Release 均为 `0 warning / 0 error`。
- 当时后续固定计划是闭合 `tree_moss_harvest_sample / full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest`。必须复用现有 `foraging.harvest_tree_moss` 与共享移动/自然拾取链；通过后重建 `26/26` 索引，再评估正式训练准入，不能仅凭样本通过直接授权训练。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生鱼塘产物闭环

- `fish_pond_output_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:812 / (O)812 / native_fish_pond_output / fish_pond:Sturgeon:0 / full_shipment:full_shipment:item:812:0:46`，唯一动作链仍为 `fishing.service_fish_ponds -> collect_fish_pond_output -> executor.collect_fish_pond_output`；没有新增第二套候选、编译器或执行器。
- 透明桥继续完整保留 `Sturgeon:0..5` 六条权威产出来源。当前候选索引按完整 `route_kind + source_id + qualified_item_id` 建立来源身份，并在同一实时鱼塘可对应多条静态来源时只允许字典序首个来源进入规划与派发；其他来源仍可读，但不会重复占据 Teacher 前沿。候选新增真实 `route_distance_tiles` 与 `expected_output_quality`，索引缓存 schema 升至 `v12`。
- 当前鱼塘输出已贯穿日历、位置、设施、资源、加工、随机性、日内时间/能量和派发轴。现有鱼塘、已物化 `(O)812 x1`、质量 `0`、零输入、确定性当前输出、零能量和原生收取编译链均须精确一致；来源、物品、位置、数量、质量或快照漂移继续失败关闭。
- 正式 v5 原生执行复用游戏鱼塘交互取得 `(O)812 x1`，动作回执为 `executor.collect_fish_pond_output`，初始状态哈希 `9640ddc868bab5f4aff44f9b3f065337a90685968ffeec29a2d63ff7d30b3902`，终态状态哈希 `d73e96ac09969e3587d09fb6cb495baadbc40c172a5952a7789655d4de8d5dcb`。离线重放复用同一原生执行回执并完整通过 fresh terminal、settlement 与 rollout proof，不重新启动游戏。
- 重放期间修复两个开发效率问题：`Resume-RuntimeFullShipmentAcquisitionProof.ps1` 现在可从唯一透明当前候选恢复未绑定的 option-only 排名，不再把它强制改写成显式参数候选并破坏队列哈希；canonical 鱼塘解析继续沿用历史“当前候选不存在”诊断字符串，避免无行为变化的文案调整令旧证据大面积漂移。Sap 历史位置轴重算结果为 `CHANGED=0`。
- 严格 production evidence index 已推进到 `21/26`。剩余 `5` 层为 `machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output`、`native_solar_panel_output`、`native_tree_moss_harvest`；`formal_product_training_authorized=false`。鱼塘 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fish-pond-output-v5-20261014\runtime-full-shipment-evidence-fish-pond-output-v5-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v35-20261014`。
- 下一固定切片是共享机器产出族：先审计并统一 `machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output` 与 `native_solar_panel_output` 的现有当前机器候选、设施、输入、加工、预算和 `farm.collect_machine_outputs` 执行链，再集中采证；不得为四种来源复制四套规划或执行系统。树苔在机器族之后单独闭环。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生动物产物闭环

- `farm_animal_product_sample` 与 `farm_animal_deluxe_product_sample` 已从冻结源存档的隔离副本完成原生采证。普通牛奶绑定 `full_shipment:item:184 / (O)184 / native_farm_animal_produce / farm_animal:White Cow:0`；大壶牛奶绑定 `full_shipment:item:186 / (O)186 / native_farm_animal_deluxe_produce / farm_animal:White Cow:0`。
- 两层严格复用唯一动作链 `farm.collect_animal_products -> collect_animal_product -> executor.collect_animal_product`。没有新增第二套候选、编译器或执行器；新增内容仅把现有当前动物候选的精确来源、位置、目标/站位格、路线距离、非消耗工具和能量证据贯穿日历、位置、设施、资源、加工、随机性与日内预算轴。候选索引缓存 schema 升至 `v11`。
- 普通牛奶原生回执在 `97` tick 内完成，验证 `current_produce -> null`、银星 `(O)184 x1`、能量 `270 -> 266`、耕种经验 `0 -> 5`、友好度 `500 -> 505`、产奶统计 `0 -> 1`，状态哈希与终态快照均新鲜。大壶牛奶使用同一执行链和 `DeluxeProduceItemIds` 权威来源通过完整 fresh terminal、settlement 与 rollout proof。
- 严格 production evidence index 已推进到 `20/26`，剩余 `6` 层：`machine_output`、`native_fish_pond_output`、`native_machine_flavored_output`、`native_machine_item_query_output`、`native_solar_panel_output`、`native_tree_moss_harvest`。共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v32-20261014`。
- 现有证明预检仍需逐条重建 receipt，约需数分钟；在 #200/#201/#202 的 26 层门槛满足前，不以跨 proof 缓存削弱验真。`Invoke-RuntimeFullShipmentEvidenceMilestone.ps1 -CompactOutput` 现只返回状态、计数、剩余层与完整文件路径，避免把数百行 checkpoint/index 倒灌控制器上下文；同族路线完成后集中刷新一次严格索引。
- Bootstrap 与 RuntimeTestHarness Release 均 `0 warning / 0 error`，Core `2680/2680`、Backend `229/229` 全量通过。下一固定切片是 `fish_pond_output_sample / full_shipment:item:812 / (O)812 / native_fish_pond_output`，优先复用现有 `fishing.service_fish_ponds` 透明、候选、编译和原生收取链。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生水果树产物闭环

- `fruit_tree_harvest_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:638 / (O)638 / native_fruit_tree_produce / fruit_tree:628:0 / full_shipment:full_shipment:item:638:0:0`，继续复用唯一的 `foraging.harvest_fruit_tree -> harvest_fruit_tree -> executor.harvest_fruit_tree` 链，没有新增第二套候选、编译器或执行器。
- 当前精确水果树候选现将来源、地点、目标格、站位格和真实路线距离贯穿到日历、位置、随机性、设施、加工与日内时间/能量轴；同物品、同路线族的非当前水果树来源在存在精确当前来源时判为不适用。候选索引缓存 schema 升至 `v10`。
- v1 在 Teacher preference 前失败关闭，暴露设施容量与加工轴尚未识别当前现成果树；v2 证明规划和原生摇树成立，但回执错误地把“果实生成在 debris”当成“采收完成”。执行器现改为两阶段：先验证原生 `GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake` 产出，再复用共享 BFS、正常移动输入和游戏碰撞机制自然拾取；不直接写 debris 或背包。
- 正式 v3 在 `93` tick 内完成，回执为 `applied/verified`，验证 `fruit_count 1 -> 0`、`(O)638` 品质 `0` 数量 `1`、背包 `0 -> 1`、觅食经验不变，以及 fresh terminal、settlement 和完整 rollout proof chain。rollout manifest SHA-256 为 `3c599efe1e7510c2ffa37a5e3e8e95bbcb0070a8fe135f0dd643c3ef279481ba`，receipt SHA-256 为 `1ce50884e8f31c9a614867f17be28dbb66e73758db931aafed7dcca0a4a44e55`。
- 严格 production evidence index 已推进到 `18/26`，剩余 `8` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fruit-tree-v3-20261014\runtime-full-shipment-evidence-fruit-tree-v3-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v30-20261014`。
- Bootstrap 与 RuntimeTestHarness Release 均 `0 warning / 0 error`，Core `2679/2679`、Backend `229/229` 全量通过。当时后续计划先优化单样本 1599 路线全量重算及严格索引重复验真，不得削弱 fresh snapshot、原生执行、终态回执或 proof 哈希门槛；随后进入 `farm_animal_product_sample / native_farm_animal_produce`。
- 即时开发效率优化先消除 `quality` 在功能分支 push 与 PR 上的重复触发：分支只由 `pull_request` 验证，`main` push 继续完整验证，并以 workflow + PR/ref 为并发键取消同一分支的陈旧运行。精确 requirement 级路线裁剪与跨 proof 证书复用仍受 #200/#201/#202 的 26 层启动门限制，不提前削弱；仅执行器发生变化且初始 StateHash 与规划输入不变的重试，应使用现有 `ReplayAcquisitionQueue` 入口复验原生执行和后续 proof。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生地点觅食拾取闭环

- `location_forage_spawn_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:16 / (O)16 / native_location_forage_spawn / location:Forest:0 / full_shipment:full_shipment:item:16:0:2`，继续复用唯一的 `foraging.collect_spawned_objects -> collect_spawned_object -> executor.collect_spawned_object` 链；没有新增第二套候选、编译器或执行器。
- v1 在正式执行前失败关闭并暴露两处真实规划缺口：当前精确地点觅食候选尚未进入日内时间/能量轴，同物品、同路线族的非当前静态来源也没有在存在精确当前替代来源时判为不适用。修复后，候选把地点、目标格、站位格和真实路线距离贯穿到位置与日内预算；随机轴按完整 `route_kind + source_id + qualified_item_id` 绑定当前来源，并排除同路线的非当前替代来源。候选索引缓存 schema 同步升至 `v9`。
- v2 原生动作经 `GameLocation.checkAction` 在 `261 -> 265` game tick 内从 `Forest (1,6)` 取得 `(O)16 x1`，回执为 `applied/verified`；验证目标对象移除、背包 `0 -> 1`、觅食经验 `0 -> 7`、耕种经验 `0 -> 0`、输出品质 `0`、fresh terminal receipt、settlement 与完整 rollout proof chain。没有直接删除对象、写背包或写经验。
- rollout proof manifest SHA-256 为 `6aad341921b5b081064ecb0865556e38582b2e1d55e219c7ab0da50858e5068b`，receipt SHA-256 为 `31d5347d06e9d607532af4e1c80150f170eb2b40b089bd33077024bede49383b`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `17/26`，剩余 `9` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-location-forage-v2-20261014\runtime-full-shipment-evidence-location-forage-v2-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v29-20261014`。
- 本轮单样本仍触发 1599 条路线的目标日期轴重算，17 层索引也需跨进程重验全部 proof；它们是 #195/#200/#201/#202/#211 已登记的开发效率瓶颈。当前不能以缓存跳过 fresh snapshot、原生执行或终态回执，但后续应使用依赖 DAG 与可失效证书缩小重算范围。下一固定切片是 `fruit_tree_harvest_sample / native_fruit_tree_produce`。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生春葱采收闭环

- `spring_onion_harvest_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:399 / (O)399 / native_spring_onion_harvest / Crop.harvest`，继续复用唯一的 `foraging.harvest_spring_onions -> harvest_crop_tile -> executor.harvest_crop` 链；没有新增第二套候选、编译器或执行器。
- 透明桥仅为原版 `Crop`、`(O)399` 且投影状态为 `exact_from_decompiled_native_spring_onion_branch` 的当前可收获作物发布权威路线来源。当前候选将地点、目标格、站位格、BFS 前置所需的移动上限、路线距离和完整来源身份贯穿到日历、位置、日内时间/能量、Teacher 与动作队列；来源、物品、地点、格子或预算漂移继续失败关闭。
- 原生动作在 `261 -> 264` game tick 内取得 `(O)399 x1`，回执为 `applied/verified`；验证当前作物从 ready 变为移除、背包 `0 -> 1`、耕种经验 `0 -> 0`、觅食经验 `0 -> 3`、fresh terminal receipt、settlement 与完整 rollout proof chain。动作仍由游戏原生 `Crop.harvest` 产生物品与经验，没有直接改背包或经验。
- rollout proof manifest SHA-256 为 `7976da974cbec8c931e1b52d137f09f2891336864e31e82fe4c72d9787335677`，receipt SHA-256 为 `da94fcddbd89a96e11395c0142b2852075cbf1728250ce22b077c8e6ce750057`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `16/26`，剩余 `10` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-spring-onion-v2-20261014\runtime-full-shipment-evidence-spring-onion-v2-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v28-20261014`。
- 候选索引缓存 schema 随新增春葱路线升至 `v8`，避免同一进程内误复用旧候选集合。下一固定切片是 `location_forage_spawn_sample / native_location_forage_spawn`；只复用现有地点觅食、移动和自然拾取链补齐严格 proof，不重跑已通过的 16 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生野树种子闭环

- `wild_tree_seed_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:88 / (O)88 / native_wild_tree_seed / wild_tree:6 / full_shipment:full_shipment:item:88:0:1`，继续复用唯一的 `foraging.harvest_tree_product -> executor.harvest_tree_product` 链。
- `(O)88` 的权威清单同时包含 `wild_tree:6` 与 `wild_tree:9`。当前 fixture 只物化 `wild_tree:6`；规划器现在用 `route_kind + source_id + qualified_item_id` 绑定可执行候选，同时允许由锁定的 `Data/WildTrees` 证明同物品、同路线族的另一来源无额外日历限制。位置轴仍要求精确 `source_id` 的实时树候选，因此缺席的 `wild_tree:9` 明确为不适用，不会借用 `wild_tree:6` 冒充执行。
- 最终 v2 原生动作在 `70` tick 内经 `GameLocation.checkAction -> Tree.performUseAction -> Tree.shake` 取得 `(O)88 x1`。回执为 `applied/verified`，验证 `has_seed true -> false`、`was_shaken_today false -> true`、完整输出域、背包自然拾取、觅食经验 `0` 和安全槽位恢复；没有新增第二套候选、编译或执行系统。
- v1 仅用于暴露 `wild_tree:9` 日历占位导致的 Teacher 分母缺口，不属于正式证据。最终规则落地后先重建既有 `14` 层确认无 proof 漂移，再运行 v2 并构建最终索引。
- 最终 rollout proof manifest SHA-256 为 `c83584b99a8602b7c449a92135f137325adffff74411aeadc3c05036ab4191d7`，receipt SHA-256 为 `7c22379b83bec6cdaa9f367ec37ec0575bd6a1c353bb861ca3852858c64353c2`。冻结源存档 SHA-256 仍为 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `15/26`，剩余 `11` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v26-20261013`。
- 下一固定项为 `spring_onion_harvest_sample / full_shipment:item:399 / (O)399 / native_spring_onion_harvest`，按 sample plan 顺序复用既有春葱透明读、作物采收编译与原生执行链，只补严格 Full Shipment proof。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生野树种子掉落闭环

- `wild_tree_seed_drop_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop / wild_tree:2:0 / full_shipment:full_shipment:item:408:0:4`，唯一动作链仍为 `foraging.harvest_tree_product -> executor.harvest_tree_product`。
- 严格路线轴现在按 `route_kind + source_id + qualified_item_id` 绑定当前野树产物候选，并从锁定的 `Data/WildTrees payload.2.SeedDropItems[0].ItemId` 解析来源。路线距离进入日内预算，原生摇树后的自然碎片拾取获得独立时间余量；没有增加第二套野树候选、编译器或执行器。
- `(O)408` 同时有 4 条地点觅食来源。它们未在当前快照物化，而精确的野树确定性来源已经存在，因此随机性轴将这些即时不可执行的替代路线标为不适用；若没有当前确定性替代来源，原有精确概率缺失门仍会失败关闭。该规则避免伪造概率，也没有放宽完整 Teacher 分母。
- 最终 v4 原生动作在 `56` tick 内经 `GameLocation.checkAction -> Tree.performUseAction -> Tree.shake` 取得 `(O)408 x1`。回执为 `applied/verified`，验证 `has_seed true -> false`、`was_shaken_today false -> true`、完整输出域、背包自然拾取、觅食经验 `0` 和安全槽位恢复；没有直接写树、RNG、碎片、背包或技能状态。
- v2 暴露了同需求替代觅食路线令 Teacher 分母不完整；v3 证明动作可行，但随后发现共享位置轴改写会使历史 proof 漂移，因此只保留为诊断产物。最终实现收窄到精确身份和随机性轴后，历史 `13` 层全部重建通过，再由 v4 形成正式 proof。
- 最终 rollout proof manifest SHA-256 为 `5ccc6aed8f59a0f6f061b834f8ae16ac6eb5ebd8df76139a49c3f2b581dacce9`，receipt SHA-256 为 `44516df2daebcbfbfcfa15ea4ab21a6cb17aea837e9856c355fdc08dac97a9d6`。冻结源存档 SHA-256 仍为 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `14/26`，剩余 `12` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v24-20261013`。
- 下一固定项为 `wild_tree_seed_sample / full_shipment:item:88 / (O)88 / native_wild_tree_seed`。应复用本轮同一候选、严格路线轴、原生摇树和自然拾取链，只验证 `Data/WildTrees payload.6.SeedItemId` 的独立来源层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生茶树采收闭环

- `tea_bush_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:815 / (O)815 / native_tea_bush_harvest / Bush.GetShakeOffItem / full_shipment:full_shipment:item:815:0:0`。
- 本轮没有新增业务代码。茶叶原样复用浆果切片统一后的透明桥、`foraging.harvest_bushes -> executor.harvest_bush` 候选/编译/执行链、共享灌木 fixture、BFS 移动、自然碎片拾取和严格终态回执，证明茶叶与浆果没有形成两套系统。
- 原生 `checkAction` 在 `39` tick 内取得 `(O)815 x1`，回执为 `applied/verified`；验证灌木偏移复位、原生输出增量、背包自然拾取和茶叶分支原生觅食经验 `0`。没有直接写背包或调用碎片直收 API。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。茶叶 proof 根位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-tea-bush-v2-20261013\runtime-full-shipment-evidence-tea-bush-v2-20261013`。
- 严格 production evidence index 已推进到 `13/26`，剩余 `13` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v19-20261013`。
- 首次运行因合并后源码时间戳晚于 DLL 被 `-SkipBuild` 新鲜度门禁拒绝，未启动游戏或生成证据；完成受约束组件重建后只运行一次茶叶样本和一次最终索引。该门禁按设计阻止旧二进制证据，不能为提速绕过。
- 下一固定项为 `wild_tree_seed_drop_sample / native_wild_tree_seed_drop`。应先审查并复用现有野树摇落/掉落、移动与自然拾取链，只补当前严格路线轴和 Full Shipment proof；不得重跑已通过的 13 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生姜采收闭环

- `ginger_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:829 / (O)829 / native_ginger_harvest / Crop.hitWithHoe / full_shipment:full_shipment:item:829:0:0`，继续复用唯一的 `foraging.harvest_ginger -> executor.harvest_ginger` 链，没有新增第二套规划、编译或执行系统。
- 透明桥只为精确原版 `HoeDirt/Crop/Hoe` 暴露 `native_ginger_harvest / Crop.hitWithHoe / (O)829` 权威来源。当前候选、日历、地点、日内时间/能量、Teacher 和调度编译均绑定同一个当前快照候选；来源、日期、地点、格子、工具、输出或预算漂移继续失败关闭。
- 执行器保留原生锄头生命周期。锄出姜后复用现有 BFS/移动原语靠近原生碎片，由游戏自身磁吸进入背包；不直接移除作物、不直接增加经验、不调用碎片直收 API，也不直接写背包。最终动作在 `47` tick 内取得 `(O)829 x1`，回执为 `applied/verified`，并验证作物移除、HoeDirt 状态、原生碎片生成、觅食经验 `+7` 和自然拾取。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。姜 proof 根位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-ginger-v1-20261013\runtime-full-shipment-evidence-ginger-v1-20261013`。
- 严格 production evidence index 已推进到 `12/26`，剩余 `14` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v18-20261013`。
- 本轮只执行一次原生样本和一次最终 evidence-index 构建，没有重跑已通过的 11 层。后续开发性能继续遵守 #195/#200/#201/#202/#211/#223：相同确定性输入单次计算，完整传递身份和哈希，证据按依赖失效；不能用缓存绕过 fresh snapshot、原生执行或严格终态回执。
- 下一固定项为 `tea_bush_harvest_sample / native_tea_bush_harvest`。应复用现有 `foraging.harvest_bushes -> executor.harvest_bush` 链和共享灌木 fixture，只补茶叶当前候选的严格路线轴与 Full Shipment proof，不得复制浆果执行器，也不得重跑已通过的 12 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生浆果灌木闭环

- `berry_bush_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:296 / (O)296 / native_bush_shake / Bush.GetShakeOffItem / full_shipment:full_shipment:item:296:0:0`，继续复用唯一的 `foraging.harvest_bushes -> executor.harvest_bush` 链，没有建立第二套收获执行器。
- 透明桥现在为当前可收获灌木暴露精确权威来源；当前候选索引、日历、地点、日内时间/能量、Teacher、调度编译和执行绑定均按同一候选身份闭合。普通浆果和茶树只在当前快照存在精确候选时进入对应日历；来源、物品、地点、格子或参数漂移继续失败关闭。
- 夹具站位按灌木真实像素包围盒外围计算，避免两格宽灌木把锚点相邻格误当成可站立格。原生 `checkAction` 后，执行器先验证灌木状态、掉落守恒、经验和核桃分支，再复用现有 BFS 与自然磁吸语义收集落在灌木另一侧的碎片；不会直接写背包或调用直接收集 API。最终动作在 `52` tick 内取得 `(O)296 x3`，回执为 `applied/verified`；候选按包含自然拾取的保守 `180` tick 交互预算参与日内时间校验。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`，原生收获、精确库存增量、装箱、睡眠、次日 Full Shipment 结算和 proof chain 均已验证。冻结源目录哈希保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `11/26`，剩余 `15` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v17-20261013`；本轮原生证明位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-berry-bush-v16-20261013\runtime-full-shipment-evidence-berry-bush-v16-20261013`。
- `-SkipBuild` 现在会校验 Backend、ProductExecutor、LiveTrainingLoop 与 GoalConditionedBootstrap 的 Release 输出新鲜度，防止在线排名器与离线验证器因旧二进制产生证据形状分裂。当前主要开发性能瓶颈仍是既有证据索引和 1599 条路线的目标日期轴跨进程重复重算；后续优化必须沿 #195/#200/#201/#202/#211 的依赖哈希与可失效 proof 复用方向推进，不能削弱 fresh snapshot、原生执行或严格回执。
- 下一固定项为 `ginger_harvest_sample / native_ginger_harvest`。应先复用现有 `foraging.harvest_ginger -> executor.harvest_ginger` 链并只补当前严格采证缺口；不得重跑已通过的 11 个分层，也不得把隔离重放摘要当作正式证据。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：普通作物收获

- standard 批次首项 `parsnip_harvest_sample` 已从冻结源存档的隔离副本重新完成原生采证。Teacher 保持选择 `full_shipment:item:24 / (O)24 / harvests_as / full_shipment:full_shipment:item:24:0:0`，动作队列复用唯一的 `farm.maintain_crops -> executor.harvest_crop` 链。
- 原生执行获取 `(O)24 x1`，冻结源目录哈希保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`；rollout 状态为 `verified_complete_rollout_proof_chain`，portfolio completion 与 proof chain 均为 `true`。
- 严格 production evidence index 已推进到 `10/26`，共享 shipping/settlement 证明有效，剩余 `16` 层，`formal_product_training_authorized=false`。检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v6-20261013`。
- 下一固定项为 `berry_bush_harvest_sample / native_bush_shake`。不得重跑已通过的十个分层；优先复用现有 `foraging.harvest_bushes -> executor.harvest_bush` 链并只修真实严格证明缺口。

## 历史记录 · 2026-10-13 Full Shipment 社区中心奖励闭环与高风险批次完成

- `creates_reward_item` 现已贯通当前透明候选、日历、地点、材料、日内时间/能量、Teacher、动作队列与原生执行绑定。代表路线固定为 `full_shipment:item:336 / (O)336 / full_shipment:full_shipment:item:336:0:0 / bundle:Bulletin Board/33:reward`，继续复用唯一的 `community_center.donate_bundle_items -> executor.claim_community_center_bundle_reward` 链，没有第二套奖励执行器。
- 解析器只在当前快照精确枚举出奖励候选且 `world_progress.community_center` 的 bundle 分母完整时启用；缺少当前候选的旧快照继续保留原 `blocked_pending_route_kind_calendar_parser` 结果。该兼容门避免全局日历 JSON 无意义漂移，也使现有八份冻结原生证明在当前代码下原样通过严格重建。
- 原生样本从完成但未领奖的 Bulletin Board bundle 出发，奖励状态由 `true -> false`，Gold Bar `(O)336` 由 `0 -> 5`，动作回执为 `applied`。完整 rollout proof 为 `verified_complete_rollout_proof_chain`，portfolio 完成与 proof chain 均已验证；正式训练标志仍为 `false`。
- 回放脚本现会在非 Sap acquisition sample 成功后验证冻结源哈希并直接生成样本摘要，不再误入 Sap 专属投递/睡眠路径。里程碑脚本在高风险证据已齐时写入 `high_risk_evidence_verified`，并把空待办规范化为真正的空数组。
- 严格 production evidence index 已推进到 `9/26`，共享 shipping/settlement 证明继续有效，剩余 `17` 层；七个 high-risk 样本已全部完成。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v5-20261013`。下一固定开发项是标准批次首项 `parsnip_harvest_sample`，不得重复运行已通过的高风险样本。

## 历史记录 · 2026-10-13 Full Shipment 商店证据入库与 Sap 结算证明重基

- Full Shipment 运行时样本计划中的商店代表已从过时的 `full_shipment:item:388 / (O)388` 修正为原生 v8 实际验证的 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。`Test-FullShipmentRuntimeSamplePlan.ps1` 重新对账为精确 `26/26`，商店层候选路线包含已验证的 `full_shipment:full_shipment:item:380:0:10`。
- 当前编译器增加队列内位置投影后，旧 Sap Teacher receipt 按完整编译语义正确失效。Sap 已从 fresh 隔离存档重新执行 acquisition、发货、回家、原生睡眠与次日结算；新 checkpoint 为 `prefix_proof_verified=true`、`verified_iteration_count=1`、`remaining_item_count=153`、`ready_for_next_iteration=true`。
- Sap 回家后的快照处于淡入阶段，睡眠队列前快照已恢复玩家控制，两者相隔 `41` tick。两份快照的实际差异仅为淡入/控制门、功能投影 tick/指纹和机器探针缓存 tick；游戏内时间、位置、Full Shipment 进度及待结算 Sap 均未变化。recurrence verifier 不再错误要求这两个独立采样快照整份哈希相同，改为同时验证各自 state hash、tick 单调性、角色/存档身份、游戏内时间、位置、完整收集进度和精确待结算数量；位置、进度、发货箱或哈希漂移负例均失败关闭。
- 严格 production evidence index 已从冻结原生证据重建为 `8/26`：共享 shipping/settlement 证明有效，商店购买已作为 `verified_exact_native_sample` 入库，剩余 `18` 层。高风险批次当时只缺 `community_center_reward_sample`；检查点位于 `F:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v4-20261013`。
- 本轮原生 Sap 重基完成后，checkpoint 与 evidence export 均通过；随后 evidence preflight 只读冻结证据，没有重复运行商店样本。`formal_product_training_authorized=false` 保持不变。下一固定开发项为社区中心奖励层，之后再进入其余标准分层。

## 历史记录 · 2026-10-12 #224 Teacher 构建去重与 Full Shipment 原生商店购买闭环

- Grandpa 目标分母已按权威目录固定为 `19` 个评分准则、`11` 个根方法，不再把方法数误当准则数。Goal-method 对账 schema 升至 `v3`，新增 `10` 个推进方法族和 `19` 个准则视图，其中 Money、Friendship、Skill 共 `3` 个单调阈值族共享各自推进过程，同时保留每个准则的独立终态断言与 Teacher 目标上下文。
- `GoalMethodCoverageReconciliationBuilder` 现在只构建一次权威 frontier，旧路径的 `2` 次降为 `1` 次；理论独立推进构建由 `19` 次合并为 `10` 次，避免 `9` 次确定性重复构建。以上是结构计数，不冒充 wall-time 实测。当时仍只有 `4/19` executable、`2/19` coverage-ready，`formal_product_training_authorized=false` 不变。
- 历史回归常量已从过时的 `877` 条对齐到当前权威 `1382 resolved / 217 blocked`；完整 `experiments/Run-Regression.ps1` 已通过。商店购买 supporting transition 现覆盖第六类 `shop_purchase_navigation`，统一复用既有路线、柜台交互、购买和关菜单原语，没有第二套执行器。
- 原生 v8 回放 `runtime-full-shipment-shop-purchase-20261012-stage-loop-v8` 已从 fresh 隔离存档完整通过三段跨图、Blacksmith 内柜台交互、ShopMenu 安全计时等待、精确购买和菜单关闭。终端队列为 `executor.wait_ticks -> executor.buy_shop_item -> executor.close_menu`，三项均为 `applied/verified`；金额 `500g -> 350g`，`(O)380` 铁矿 `0 -> 1`，菜单终态为关闭。
- 编译器只允许 `ShopMenu + 正安全计时器 + 非只读/无手持物 + 完整购买延续身份 + 唯一阻塞理由为安全计时器` 的有界等待；后续购买必须紧邻同一身份等待，并在原生执行时再次检查菜单、计时器、商店、库存、价格和容量。身份漂移或缺少安全约束继续失败关闭。Core game-free `288/288`、GoalConditionedBootstrap Release `0 warning / 0 error`、acquisition dispatch 与商店导航终端覆盖自测均通过。
- 近期开发过慢确有一部分来自重复确定性 frontier/推进构建、逐路线空间扫描、重复钓鱼概率投影以及陈旧 fixture/计数造成的无效回归；这些已分别去重或修正。原生 fresh snapshot、执行前安全门、终态回执和 rollout proof 属于必要验证，不能按性能优化名义删除。

## 历史记录 · 2026-10-11 Full Shipment 高风险证据与严格投影优化

- 当时权威 evidence index 已从锚点阶段的 `2/26` 推进到 `7/26`。新增通过 `native_wild_tree_tapper_output`、`native_monster_drop_table`、`native_location_fish_spawn`、`native_location_artifact_spot` 与 `native_geode_drop` 五个 exact native sample；对应树液收集器产物 `(O)725`、怪物掉落 `(O)766`、地点钓获 `(O)388`、古物点黏土 `(O)330` 和 Golden Coconut `(O)791` 精确开出的 5 个铱矿 `(O)386` 均完成原生执行、fresh terminal、settlement、checkpoint 与 rollout proof 重建。共享 shipping/settlement 证据继续有效，剩余 `19` 层，`formal_product_training_authorized=false` 不变。
- 古物点路线现在从当前透明快照精确绑定地点、目标格、站位格、移动距离、锄头次数、能量和确定性产出；静态 `Default` 掉落规则只在存在匹配的当前古物点时解析为实际地点。动作编译继续复用唯一的 `executor.clear_obstacle`，并在同一队列追加既有 `executor.pickup_debris`；规划编译、动作队列编译和运行执行三层均重新核对权威来源、物品、品质、数量与掉落物基线。零移动场景允许省略 `move_to_tile`，但仍严格核对教师动作时间，不允许瞬移或直接改背包。
- 晶球路线只在当前透明快照存在精确 `processing.crack_geode` 候选时启用 `Data/Objects` 来源解析，并由同一候选贯通 Blacksmith/Clint 柜台设施、背包晶球槽位、25g 服务费、原生 RNG 精确输出和日内时间预算；动作编译与执行继续复用唯一的 `executor.crack_geode`。调度器从权威 `geode:<id>:<row>[:random:<index>]` 来源重建晶球输入，并从需求集规范化用途标签，防止排名参数与防篡改重建漂移。
- 本轮还修复了跨运行恢复缺陷：同一路线的 active material/currency reservation 只有 `SourceStateHash` 与当前快照一致时才算 `claim_already_committed`；执行前中断留下的旧声明会走既有 `claim_replacement_required` 原子 upsert 路径，保留稳定 reservation ID 和完整账本历史，不会被空 portfolio marker 错误继承。
- 钓鱼场景暴露的主要瓶颈不在游戏动作层。validation trace 显示一次严格 proof 的约 `151.6 s` 中，约 `131.5 s` 用于对 `1599` 条路线、`920` 个可钓水格和鱼类规则重复做 terminal probability projection。现在每个冻结 fishing forecast 只解析一次水格、规则适用格和玩家站位限制，并以“目标物品 + 水深 + 规则适用集合 + 站位许可集合”为完整等价键复用概率；当前格坐标、站位坐标和结果元数据仍逐候选生成。
- 同一钓获 rollout proof 的严格重建从约 `151 s` 降到 `20.5 s`，新旧 receipt JSON 完全相等且 `proof_chain_verified=true`。包含现有 5 条 proof 的 evidence index 重建从约 `219 s` 降到约 `81 s`。这些缓存只存在于当时 forecast/当时 CLI invocation；不跨进程、run 或 snapshot，不跳过 authority 哈希、fresh-terminal、settlement 或 rollout 校验。
- 所有 acquisition target-date 轴现通过既有 `InvocationDeterministicBuildSession` 做同一命令内的内容哈希去重，命中后重新反序列化独立 DTO，避免共享可变结果。GoalConditionedBootstrap Release 为 `0 warning / 0 error`，Core game-free 基线为 `276/276`，Bootstrap hermetic、Full Shipment runtime evidence 与 acquisition dispatch 自测均通过。下一证据顺序为商店购买、社区中心奖励；已通过的地点蚯蚓与晶球掉落不得重复运行。

## 历史记录 · 2026-10-11 Full Shipment 当时权威锚点恢复

- Sap 原生回放现在通过独立 `stardewai.runtime_full_shipment_sap_evidence_export.v1` 出口进入里程碑。该出口会重新深度构建 recurrence-prefix checkpoint，并精确绑定 Sap `full_shipment:item:92 / (O)92 / native_wild_tree_chop_drop`、rollout manifest/receipt 及四个文件哈希；它不复用旧 smoke 摘要的声明，也不能授权训练。
- 放射性矿石历史原生执行证据已按当前 requirement、lowering、Master Angler 窗口和路线计时输入完整重建。修复内容包括：确定性构建键允许空的可选路径值；恢复脚本在 Teacher 前和 ledger commit 后都重新请求当前 ranking；普通下矿候选不再输出三个空的 acquisition 意图字段，显式怪物掉落意图仍要求三个字段完整一致。旧原生运行的两步 `executor.mine_stone -> executor.pickup_debris` 没有改写，当前 fresh-terminal、settlement、checkpoint 与 rollout proof 已全部重新验证。
- 当时权威 evidence preflight 为 `2/26`：Sap 与 `native_radioactive_ore_node` 两个 anchor 均为 `verified_exact_native_sample`，共享 shipping/settlement 证明有效，剩余 `24` 层。下一批固定为 7 个 high-risk 场景：树液收集器、怪物掉落、地点钓获、地点蚯蚓、晶球掉落、商店购买、社区中心奖励。
- `formal_product_training_authorized=false` 保持不变。本轮只读取和重建 F 盘冻结证据并启动隐藏 Backend，没有启动游戏、修改存档或产生正式训练样本。当时机器可读检查点位于 `F:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v2-20261011`。

## 历史记录 · 2026-10-11 #214 snapshot 派生空间缓存正确性

- `SnapshotStateCache<T>` 统一替换 collision grid、route connector/route-repair 候选与清障编译验证索引的裸 `ConditionalWeakTable<SnapshotEnvelope,...>`。命中现在必须同时满足同一快照引用和同一非空 `StateHash`；空 hash 永不复用，构建过程中 hash 改变立即失败关闭。
- 同一个 `SnapshotEnvelope` 原地替换 `State/StateHash` 后，路线候选会从碰撞阻塞重建为可用，清障编译不会继续读取旧对象索引，机器放置也会按新碰撞格阻断；hash 不变时仍只构建一次。没有新增第二套路由、候选或编译逻辑。
- game-free Core 回归扩展到 `274/274`，Bootstrap hermetic 通过，Core 与 GoalConditionedBootstrap Release 均为 `0 warning / 0 error`。F 盘冻结 execution-binding 复算约 `14.149 s`，旧/新 SHA-256 同为 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`；fresh snapshot、runtime dispatch 和 verifier 门禁未降低。

## 历史记录 · 2026-10-11 #200 Phase A observe-only dependency DAG

- validation trace 新增独立子 schema `stardewai.validation_dependency_graph.v1`。节点记录 artifact kind/id、输出 SHA-256、producer/version、输入 artifact/hash/path、reuse scope、调用/缓存观察和 wall/CPU 成本；嵌套重算形成真实依赖边，并从每个外部输入计算理论失效后代。
- 图已覆盖 calendar resolution、全部 target-date 轴、opportunity cost、Teacher preference、portfolio admission/commit、dispatch validation、execution binding、fresh terminal、settlement、checkpoint 和 rollout proof；没有建立第二套 planner/compiler/executor/verifier。固定安全字段为 `observation_only=true`、`can_affect_admission=false`、`incremental_reuse_authorized=false`。
- F 盘冻结 Full Shipment Sap recurrence-prefix 对照输出单根、25 个 producer 节点、42 个外部输入失效集合；初始 snapshot 漂移会理论失效全部 25 个后代。输出 SHA-256 仍为 `98222532fcb4df7b16847311159095bf5941e87f12188d2b19290906db8d6f12`，状态仍是 `verified_recurrence_prefix` 且 `prefix_proof_verified=true`。
- 独立 Teacher supervision 链现复用同一 rollout proof，并新增 `training_sample_admission_certificate -> teacher_dataset_certificate` 两个观察节点。冻结一 transition 终态 rollout 输出单根、27 个节点、44 组外部输入失效集合；初始 snapshot 漂移理论失效全部 27 个节点，仅 proof manifest 漂移则精确失效 proof、rollout admission 与 Teacher dataset 三层。带 trace 与无 trace 的数据集 SHA-256 均为 `c91a75874801bb2d8e1c7459de35862ed0605aeb876a3ba6a563f5bcd90b4579`，状态仍为 `ready_verified_portfolio_supervision_dataset`，`formal_product_training_authorized=false`。
- observe-only 图的完整哈希和失效解释令本次显式 trace 从上一轮约 `24.340 s` 增至约 `29.200 s`；直接 Release 无 trace 回放为 `24.927 s` 且 SHA 不变，默认路径不执行图构建。Phase A 不允许跳算、proof inheritance、跨 run 缓存或 selective native replay，`formal_training_authorized=false` 不变。详见 `docs/validation-dependency-dag-phase-a.md`。

## 历史记录 · 2026-10-11 #195 路线清障与空间索引深度优化

- Windows CPU 采样确认剩余热点不在游戏或模型，而在 `CandidateOptionAvailabilityEvaluator`：旧实现对每个“被阻挡出口 × 可清障物”分别重读碰撞 JSON，并在清障前后各跑一次全图 0-1 BFS。现改为同一快照先计算一次起点距离场，再按唯一出口计算反向距离场；一个障碍当且仅当位于某条最小清障数路径上时生成原有 route-repair 候选。候选顺序、字段、阻塞理由和 `clear_one_obstacle_then_fresh_snapshot` 策略不变。
- 碰撞格和 `ActionQueueCompiler` 的地形/对象校验新增以 `SnapshotEnvelope` 实例为弱键的只读坐标索引，消除每个障碍再次线性扫描整个数组；索引不跨快照。location、stochastic-retry、daily-time-energy 三个轴重复读取的当前路线候选与野树候选，则只在既有 `InvocationDeterministicBuildSession` 中按精确快照 SHA 复用内部不可变索引，不跨命令、run 或 snapshot。
- 同一冻结 1599-route Sap 输入上，execution-binding 从上一轮 `104.668 s` 继续降为 `13.475 s`，相对本轮起点缩短约 `87.1%`；旧/新输出 SHA-256 都是 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`。真实消费入口 recurrence-prefix checkpoint 从 `114.952 s` 降为 `24.340 s`，仍输出 `verified_recurrence_prefix`、`verified_iteration_count=1`、`remaining_item_count=153`、`prefix_proof_verified=true`。
- GoalConditionedBootstrap Release 为 `0 warning / 0 error`，Core game-free `256/256` 与 hermetic Bootstrap self-test 通过；性能验证只读取 F 盘冻结制品，没有启动游戏、修改存档或生成训练数据。`formal_training_authorized=false` 不变。下一性能阶段仍是 #200 Phase A 的完整 DAG/成本观测；本轮不授权跨运行缓存、proof inheritance 或 selective native replay。

## 历史记录 · 2026-10-11 #195 确定性证明链单次 invocation 去重

- Full Shipment Sap 完整原生回放已经生成有效的 acquisition、deposit、sleep settlement、rollout proof 与 recurrence-prefix manifest；但旧 `build-full-shipment-recurrence-prefix-checkpoint` 在持续计算超过 12 分钟后仍未结束，开发反馈成本已不可接受。该中断不否定已落盘的原生动作与结算证据，但当时没有生成 prefix checkpoint/summary，不能宣称完整 runner 通过。
- Bootstrap 新增默认启用、可显式关闭的 `InvocationDeterministicBuildSession`。缓存只存在于当前进程/当前命令，键包含 producer/version 及每个输入文件的内容 SHA-256；不跨命令、不跨 run、不跨 snapshot。缓存值只保存序列化字节，命中时重新反序列化为新 DTO，不共享可变对象。`--deterministic-build-session disabled` 或 `STARDEWAI_DETERMINISTIC_BUILD_SESSION=disabled` 可恢复原完整重算路径。
- 首批只接入实测重复最重的四个节点：target-date processing、opportunity cost、portfolio Teacher preference 与 execution binding。每个唯一子图在本命令中仍完整反算一次；相同输入再次由 fresh/settlement/checkpoint 递归请求时才复用。validation trace 新增 `deterministic_build` operation，能按 producer 区分调用数、命中和累计 wall/CPU，不改变 admission。
- 同一冻结 1599-route Sap 输入上，execution-binding 从原运行约 `469 s` 降为 `104.668 s`，旧/新输出 SHA-256 均为 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`，缩短约 `77.7%`。此前超过 12 分钟仍未完成的 recurrence-prefix checkpoint 现于 `114.952 s` 完成，输出 `verified_recurrence_prefix`、`verified_iteration_count=1`、`remaining_item_count=153`、`prefix_proof_verified=true`。
- 性能回归只消费 F 盘冻结制品，没有启动游戏、修改存档或产生正式训练数据。GoalConditionedBootstrap Release 为 `0 warning / 0 error`，hermetic Bootstrap self-test 通过，Core game-free 为 `256/256`。本轮解除的是 proof 重复重算的开发效率硬伤；`formal_training_authorized=false` 不变。
- 当时后续计划先用现有 trace 完成 #200 Phase A 的全节点依赖/成本观测，再评估 opt-in batch Bootstrap。未建立完整 DAG 和 full-shadow 等价前，不启用跨命令增量跳算、proof inheritance 或 selective native replay。

## 历史记录 · 2026-10-11 Full Shipment Sap 原生证据恢复

- F 盘隔离实验室已经从真实原生新档生成独立 Spring 1 基线：存档槽为 `ProofFarm_451377026`，Full Shipment 初态为 `0/154`，只加载 TransparentBridge 与 RuntimeTestHarness。`Invoke-RuntimeFreshSaveSmoke.ps1` 新增可选 `OutputRoot`，默认行为不变，允许把高负载制品稳定留在 F 盘而不污染 I 盘仓库。
- 隐藏训练模式此前没有安装已有的 `HeadlessMovementInputSimulator`，导致路线、碰撞判断和当时后续计划方向均正确时角色仍停在原地。RuntimeTestHarness 现在仅在 `STARDEWAI_TRAINING_MODE=1` 且 `STARDEWAI_SUPPRESS_LOCAL_RENDER=1` 时复用同一个原生输入泵；没有新增第二套移动器、直接坐标写入或 BFS 分支。原生 FarmHouse 出门回归已通过。
- Full Shipment runner 不再硬编码历史 `ProofFarm_450250338`。未显式指定时从归档存档目录名派生槽名；显式槽名与目录叶名不一致、主存档文件不与槽同名或缺少 `SaveGameInfo` 时均在启动游戏前失败关闭。
- 新档 Sap 规划真实暴露了一个下游选择错误：`wild_tree:1:0` 有 27 个已解析原生树目标，旧日预算层只取第一个目标；该目标的二次静态寻路失败后，整条 Sap 来源被误判为机会分母不完整。现在按现有候选顺序逐个调用同一个 `EvaluateTerminal`，首个完整匹配立即返回；若没有匹配，仍只保留最佳已解析结果并维持失败关闭，不降低 Teacher 完整分母标准。
- 同一原生快照上的严格离线重算已通过：Sap `11/11` 路线无阻塞，`1` 条进入 Pareto 前沿；Teacher 为 `UniqueStrictPareto`，分母 `1` 且完整，标签可生成，阻塞为 `0`。目标树为 Farm `(49,20)`，保守完成时间 `06:53`。这只是 Sap 单层偏好证明，`formal_training_authorized=false` 保持不变，不得把离线重算冒充生产证据。
- 完整原生回放 `runtime-full-shipment-evidence-sap_prefix-20261011-150812` 已通过规划、Teacher、Sap 原生采集、投递、fresh terminal receipt 和 rollout proof，但 recurrence 的 `executor.sleep` 暴露了两个顺序故障：`DialogueBox` 的睡眠 Yes 与 `ShippingMenu` 的原生 OK 都不能由 `UpdateTicking` 阶段安装的 SMAPI 轮询按键覆盖触发。按 F 盘精确 `1.6.15` DLL 反编译结果，两处现在分别复用原生 `receiveKeyPress(Keys.Y)` 与 `receiveKeyPress(Keys.Escape)` 派发；睡眠结算和独立 ShippingMenu 恢复共用唯一的结算派发函数，没有直接改菜单、日期、出货统计或存档。
- 隔离下游重放 `runtime-full-shipment-sap-shippingfix-20261011-161922` 已真实通过：从新档副本原生获取 Sap `1`、投递并睡眠，存档从 Spring 1 写入 Spring 2，`basicShipped` 原生收据从 `0` 增至 `1`，Full Shipment 为 `1/154`，最终 `total_day=1`。该重放只复用了上一轮已经验证的 acquisition queue，用于证明执行与结算修复，不替代完整生产 recurrence 证明。
- 验证已通过：GoalConditionedBootstrap Release `0 warning / 0 error`、RuntimeTestHarness Debug `0 warning / 0 error`、Core game-free `256/256`、`git diff --check`。下一固定动作是以修复后的执行器完整重跑 Sap prefix，生成 recurrence manifest/summary 后重建 anchor evidence index，再进入既定 7 个 high-risk 场景；`formal_training_authorized=false` 保持不变。

## 历史记录 · 2026-10-11 #195 invocation-scoped immutable read session

- #199 在 Core 新增单次进程/单次命令生命周期的 `InvocationReadSession`。Bootstrap 默认只登记启动时已存在的文件参数；output/output-root、checkpoint、ledger/lease/ownership、控制参数、输出同路径与输出根后代全部禁止复用。`--read-session disabled` 或 `STARDEWAI_READ_SESSION=disabled` 可恢复原始逐次读取路径。
- `ContentAddressedJsonArtifactReader` 与 `CurrentTeacherFrontierSupport.HashFile` 共享 raw bytes、stored SHA-256 和验证后的 materialized text/logical SHA-256；内容寻址 blob 或最终 payload 只要有字节数/哈希错误仍立即失败关闭。当前 DTO 多为可变 class，因此本轮明确不共享 typed deserialize 实例，避免对象污染。
- validation trace 现在会在 operation 与 command 层真实记录 session cache hit；Phase 0 baseline builder 继续拒绝 cache-bearing trace，冻结基线须用 disabled 模式重放。反向顺序代表样本输出 SHA 完全一致，读盘由 `1464034` 降至 `913287` bytes（`37.618%`），全量哈希由 `936153` 降至 `787201` bytes（`15.911%`）；温缓存 wall 样本由 `260.714 ms` 降至 `258.003 ms`，仅作参考。详见 `docs/invocation-read-session.md` 与机器可读样本 `docs/invocation-read-session-sample.json`。
- 本轮不缓存 live snapshot、正在生成的 receipt、ownership/lease、训练准入或跨 snapshot 事实，不修改 Planner/Compiler/Executor/Verifier，不建立跨 run cache。#195 的 #200–#203 后期 DAG/增量验证顺序保持不变。

## 历史记录 · 2026-10-11 #195 本地 validation impact advisory

- #198 新增只读 `scripts/Get-ValidationImpactAdvisory.ps1`：默认仅读取 staged 与 unstaged 的已跟踪文件，也可读取显式文件清单或 `base...head` git range，输出 `stardewai.validation_impact_advisory.v1` JSON。新建未跟踪文件必须显式传入，避免把无关本地数据扫入分析。
- 映射覆盖 docs、Contracts/Core、Backend、GoalConditionedBootstrap、TransparentBridge adapter、RuntimeTestHarness、训练/产品工具、runtime scripts 与版本化 authority。未知路径、共享 runtime script、workflow/project/package 配置一律 broad fallback，并设置 `uncertain_impact=true`。
- 输出只包含本地第一轮 build/test/source-guard/native replay 建议；固定 `full_quality_still_required=true`、`evidence_reuse_authorized=false`、`native_replay_execution_authorized=false`。脚本不执行测试、不启动游戏、不继承证据，也不修改 GitHub `quality.yml`。
- game-free 行为回归直接重放 #186、#187、#192、#194、#196 的代表文件集合，并覆盖 docs-only 与 unknown fallback。下一性能切片仍是 #199 单次 invocation 内 immutable bytes/hash/materialization/parse 去重；不得扩展为跨 run 或跨 snapshot cache。

## 历史记录 · 2026-10-11 #195 Phase 0 validation trace 基线

- #197 已完成默认关闭的 validation trace：Bootstrap 可用 `--validation-trace PATH` 与 `--validation-trace-scenario NAME` 输出独立 JSON；运行时脚本仅在显式设置 `STARDEWAI_VALIDATION_TRACE_DIR` 时为子进程采集 trace。追踪失败不会改变命令结果、stdout 协议或既有 fail-closed 门禁，所有记录固定 `cache_hit=false`。
- 五个冻结代表场景已覆盖：单次低风险决策、机器 supporting-transition/portfolio、Full Shipment 单层 proof、anchor/high-risk preflight、Teacher dataset generation/relabel。聚合基线为 `complete`、`5/5`、零缓存命中，总墙钟 30.04 分钟。
- 完整 Stage 1 Teacher 链占 29.99 分钟并读取 183.27 GiB：同一命令内出现 616729 次 content-addressed JSON read、454111 次 JSON deserialize 与 857757 次 frontier file hash。热点来自严格重建的重复只读工作，不是原生游戏执行。
- 追踪记录按同一路径/producer/operation 聚合，保留首尾 operation ID、调用次数、累计时间/字节/哈希/解析成本以及有界 artifact ID，避免观测本身产生数百 MB 输出。完整链追踪由此前失败样本的 551 MB 降为约 2.1 MB；业务校验次数没有减少。
- 路线证据负例同步修正：移除全局 social route evidence 后，当前 Farm 上的 3 条原生作物路线仍可由实时当前位置证明；Beach/FixtureShop 两条跨图路线必须以 `social_route_date_evidence_unavailable` 阻断。整体仍为 partial、不可训练。完整 `self-test-current-stage-one-collection` 已以退出码 0 覆盖该边界。
- 下一顺序固定为 #198 本地 fast-check advisory，再做 #199 单次 invocation 内 immutable read/parse/hash 去重。不得先引入跨 run 或跨 snapshot cache，也不得缩小 GitHub PR 全量 quality、fresh snapshot、ownership、lease、execution receipt 或 formal-training 门禁。详见 `docs/validation-trace-phase0.md` 与机器可读基线 `docs/validation-trace-phase0-baseline.json`。

## 历史记录 · 2026-10-08 Full Shipment anchor 证明重基

- 10 月 8 日的机器首个有效输出修复把 34 条机器路线从错误的确定性通过改为显式阻塞。Sap 与放射性矿目标路线本身没有变化，但旧证明中的全局日历因此按设计失效，production evidence preflight 会以 `Calendar resolution drifted from deterministic source compilation` 失败关闭。
- `Resume-RuntimeFullShipmentAcquisitionProof.ps1 -RebuildPlanning` 现在可在不启动游戏的情况下，从原 rollout manifest 读取权威 inventory/lowering/Master Angler/计时输入和原始 `run_id`，重建日历、全部目标日约束轴、Teacher 选路、承诺、动作队列、execution binding、fresh terminal、结算和 rollout proof。它必须重新选择与旧证据完全相同的 route occurrence，并让旧原生执行回执通过新队列校验，否则立即拒绝。
- Sap 特殊链会额外重绑 recurrence manifest，并重新计算 prefix checkpoint；不是只改 summary 或文件指针。重基应在原 artifact 的副本上执行，历史证据目录保持不变。
- 异机无游戏验证已完成：Sap 和放射性矿两个重基证明均通过，随后 milestone 以 `-EvidencePreflightOnly` 独立重建 production evidence index，checkpoint 为 `anchor_evidence_verified`。下一批仍是原计划中的 7 个 high-risk 场景；`formal_product_training_authorized` 继续保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 采证里程碑计划

- 执行批次不再把“anchor 摘要及其引用文件存在”当成验真。默认 `high_risk/standard/all_missing` 会在启动首个游戏场景前，用 production evidence index 独立重建 Sap 共享出货/睡眠证明和两个 anchor 的精确 rollout proof；`all` 从零运行时则在两个 anchor 生成完、进入首个非 anchor 场景前执行同一验真。任何漂移立即写入失败 checkpoint 并停止，不消耗后续原生场景运行。
- `standard` 也不能绕过高风险批次：单独执行时要求 7 个高风险摘要齐全，并在启动游戏前用同一 production evidence index 验证其 exact native sample；`all/all_missing` 则在进入首个 standard 场景前执行同一阶段门。仅有摘要和引用文件仍不算通过。
- `-EvidencePreflightOnly` 支持 SSH/无交互桌面验真：它执行当前批次所需的 anchor 或 high-risk production index 重建，输出 checkpoint/index 后在原生 runner 循环前退出；它与仅核对计划的 `-PlanOnly` 互斥，也不能绕过缺失的前置阶段。
- 已从当前权威 requirement/lowering、动作 reconciliation 与支持终端覆盖重建 `full_shipment_static_compilability_inventory.v2`：`154` 个需求、`641` 条来源、`26` 个运行层、`0` blocker。
- `catalogs/vanilla-1.6.15/full-shipment-runtime-sample-plan.json` 为 26 层各绑定一个 runner 场景；`Test-FullShipmentRuntimeSamplePlan.ps1` 按完整执行签名核对，不把同层多来源误判为多个层，也不允许一个场景跨层。
- `Invoke-RuntimeFullShipmentEvidenceMilestone.ps1` 先执行 7 个高风险原生样本，逐场景写 checkpoint，成功可续跑、失败立即停止且不自动重试；`-PlanOnly` 只验证计划。默认高负载游戏运行目录为异机 `F:\StardewAI-TestLab\runtime`，其测试实验根仍为 `F:\StardewAI-TestLab`，本机不启动游戏。
- 里程碑编排器显式绑定异机 `F:\StardewAI-TestLab\inputs` 下的基准存档、requirement/lowering/Master Angler 输入和实验根下的移动计时校准；这些参数逐项透传给唯一原生样本 runner，不再依赖旧工作区的未提交文件或本机 I 盘默认路径。
- 原生样本 runner 的首次 Release 构建不再假设所有项目都被解决方案预恢复；每个实际入口自行恢复并构建，部署与逐项目构建输出写入本次 artifact。干净 worktree 因缺少 `obj/project.assets.json` 失败时不再只剩一个无上下文退出码。
- `RuntimeEvidenceCommon.Invoke-RuntimeBootstrap` 在 Windows PowerShell 下显式合并捕获原生 stderr，再按退出码判定；bootstrap 的结构化 JSON 错误会保留末尾诊断，不再被全局 `ErrorActionPreference=Stop` 截断为单个 `{`。
- `Invoke-InteractiveFullShipmentEvidenceMilestone.ps1` 是异机交互桌面的唯一薄入口：先确认与 Explorer 同会话，绑定 F 盘便携 SDK，并把只读权威镜像精确绑定为 inventory 已哈希绑定的 I 盘路径。已有映射仅在目标完全相同时复用且保留，目标不同则失败关闭；只有本次创建的映射才在 `finally` 撤销，进程环境始终恢复，实际工作仍委托给唯一 milestone/runner。
- 非钓鱼 acquisition 样本的 forecast manifest 现在显式写入 `snapshots: []`；不再让 Windows PowerShell 将条件表达式中的空数组折叠为 `null`，从而满足 `FishingForecastSnapshotReference[]` 的严格输入合同。
- 当时 `26/26` 是入口与采证计划覆盖，生产证据仍只有 Sap 与放射性矿石可独立复核；必须先导入/重验这两个 anchor，再跑高风险 7 层和其余标准层，最后统一构建 evidence manifest/index。训练授权仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment runtime 编排边界拆分

- `scripts/lib/RuntimeEvidenceCommon.ps1` 现统一承载场景无关的 UTF-8/JSON、目录哈希、HTTP、fresh snapshot、环境恢复、隐藏进程生命周期，以及 DailyPlan、Teacher preference、precompiled queue 的现有 LiveTrainingLoop 调用包装。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 只保留 Full Shipment 场景选择、夹具编排、权威来源绑定、Sap/出货/睡眠/recurrence 断言和对通用函数的薄参数绑定；它仍调用原有 planner/compiler/product executor/verifier，没有新增第二套运行时或脚本游戏语义。
- archived source save 哈希、隔离存档、typed queue/receipt、fresh after snapshot、checkpoint 与 `formal_product_training_authorized=false` 语义保持不变。本次是 #162 的维护性拆分，不增加 production evidence 分子；下一里程碑仍是代表性原生抽样后统一重建 26 层索引。

## 历史记录 · 2026-10-05 Full Shipment 地点钓获入口

- 最后一层代表固定为 `full_shipment:item:388 / (O)388 / native_location_fish_spawn / location_fish:Town:3`，复用现有 `fishing.catch_fish -> executor.catch_fish` 原生抛竿、上钩、小游戏和结果回执链。
- 修正了 dispatch 的旧假设：Full Shipment 中该层是地点钓鱼规则产生的非鱼物品，不能塞进 72 种鱼的 Master Angler 缺失集合。真正鱼种仍要求严格的 Master Angler 窗口；非鱼地点钓获物则必须由透明候选的完整结果分布同时证明物品、地点规则和 source index，错误物品或错误规则行继续 fail closed。
- 隔离夹具只加载 Town、装备钓竿并选择原生可钓水域，不注入 `(O)388`；runner 同时保存同一状态的需求型 fishing forecast。至此入口配置为 `26/26`，但生产 evidence index 尚未重建，正式训练仍保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 社区中心奖励入口

- 奖励物代表固定为 `full_shipment:item:336 / (O)336 / creates_reward_item / bundle:Bulletin Board/33:reward`，继续复用 `community_center.donate_bundle_items` 下已经存在的待领奖候选、DailyPlan、动作编译器和 `executor.claim_community_center_bundle_reward` 原生菜单执行链。
- 隔离夹具只把当前原生 `Data/Bundles` 中的精确 bundle 置于“已完成、奖励未领取”状态，并移动到社区中心交互点；它不会创建或注入 `(O)336`。共享 runner 必须从透明快照再次验证 bundle key、奖励物品和精确权威来源，之后产品执行器才可通过原生 Junimo Note 奖励菜单领取。
- 当时 runner 配置入口达到 `25/26`；只剩 `native_location_fish_spawn`。本轮仍不增加生产 evidence index，正式训练保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 商店购买入口

- 商店购买代表修正为 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。当前 fresh-save 权威分母的 13 条替代路线均可判定，150g 的 Blacksmith 购买是唯一 Pareto 前沿；候选显式约束 `Blacksmith`、铁矿 `(O)380`、单价不高于 150 和数量 1，继续复用 `economy.buy_supplies` 的跨图路径、柜台交互和 `executor.buy_shop_item`。旧木材 `(O)388` 代表同时存在 Town 钓鱼杂物路线，缺少对应概率证据时会被严格 Teacher 正确拒绝，不再用它伪造单一购买分母。
- 隔离夹具只使用既有 `debug.advance_time_to` 把春 1 推进到 9:00；不会打开菜单、生成物品或修改商店库存。候选来源身份由透明商店预览的 `shop_id` 验证，关闭或不可达时仍在上游排除。
- 当时 runner 配置入口达到 `24/26`；剩余 2 层为奖励物和地点鱼。本轮不增加生产 evidence index，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 晶球掉落入口

- 晶球代表固定为 `full_shipment:item:386 / (O)386 / native_geode_drop / geode:791:1:random:6`，输入为金色椰子 `(O)791`，执行继续复用 `processing.crack_geode -> executor.crack_geode` 的铁匠柜台与原生 `GeodeMenu` 链。
- 隔离夹具只搜索 `geodes_cracked_before` 以定位能产生目标的原生 RNG 前态；透明桥必须同时投影 `(O)386` 和精确来源，随后产品执行器才会敲开晶球。夹具不生成铱矿，且第一颗金色椰子固定奖励分支被显式排除。
- 该阶段 runner 配置入口达到 `23/26`，随后商店购买入口使当时总数达到 `24/26`。本轮不增加生产 evidence index，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 蚯蚓地入口

- 蚯蚓地代表固定为 `full_shipment:item:330 / (O)330 / native_location_artifact_spot / location:Default:10`，复用现有 `foraging.excavate_artifact_spots -> executor.clear_obstacle` 原生锄地链。
- 隔离夹具不会直接生成黏土；它只轮换原生 `(O)590` 蚯蚓地坐标，并通过透明桥的精确输出和权威来源投影选择可产生目标的合法前态。每次尝试前移除上一个夹具蚯蚓地，避免失败样本污染候选池。
- 该阶段 runner 配置入口达到 `22/26`，随后晶球掉落入口使当时总数达到 `23/26`。本轮仍未启动游戏，生产 evidence index 与训练准入状态不变。

## 历史记录 · 2026-10-05 Full Shipment 树液收集器与怪物掉落入口

- 野树树液收集器代表固定为 `full_shipment:item:725 / (O)725 / native_wild_tree_tapper_output / wild_tree:1:0`。夹具在同一格建立原生基础树与 `(BC)105` 树液收集器，并继续复用 `farm.collect_machine_outputs`、统一机器编译器、执行器和 verifier。
- 怪物掉落代表固定为 `full_shipment:item:766 / (O)766 / native_monster_drop_table / monster:Green Slime`。夹具只建立可归因的原生史莱姆及确定掉落，正式动作继续复用 `mining.reach_depth`、共享自动战斗和延迟拾取链。
- 该阶段 runner 配置入口达到 `21/26`，随后蚯蚓地入口使当时总数达到 `22/26`。PowerShell 解析、定向守卫 `26/26`、Core game-free `215/215` 与 RuntimeTestHarness `0 warning / 0 error` 均通过。本轮没有启动游戏，生产 evidence index 不增加，当时可独立验收的新版本原生证明仍只有 Sap 与放射性矿石，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 两类机器查询产物入口

- 已从当时权威 requirement/lowering 重新枚举 26 个运行时分层；此前脚本实际装配 17 层。本轮新增 `native_machine_flavored_output` 与 `native_machine_item_query_output`，该阶段配置入口达到 `19/26`。随后树液收集器与怪物掉落入口使当时总数达到 `21/26`。
- flavored 代表固定为 `full_shipment:item:340 / (O)340 / machine:(BC)10:rule:0:output:0`；item-query 代表固定为 `full_shipment:item:257 / (O)257 / machine:(BC)128:rule:0:output:2`。两者复用现有机器透明投影、`farm.collect_machine_outputs`、DailyPlan、编译器、产品执行器与 verifier，没有新增机器动作系统。
- 测试夹具现在显式携带原生 `lastOutputRuleId`。普通 `machine_output` 代表强制 `Default`，两类 legacy query 代表强制空值；非空规则在原生 `Data/Machines` 中不唯一时立即失败关闭，避免三个机器分层在真正运行前串线。
- PowerShell 解析通过，定向 game-free 守卫 `21/21`，RuntimeTestHarness 使用 E 盘隔离游戏引用编译为 `0 warning / 0 error`。本轮未启动游戏，因此生产 evidence index 不增加，当时可独立验收的新版本原生证明仍只有 Sap 与放射性矿石，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 原生样本收口与测试制度收缩

- 原生放射性矿石样本已通过：`runtime-full-shipment-radioactive-node-20261005-134024` 在异机 `F:\StardewAI-TestLab` 以 `(O)95 -> GameLocation.breakStone -> (O)909` 完成 `native_radioactive_ore_node`，复用 `mining.reach_depth -> executor.mine_stone -> executor.pickup_debris`，没有第二套采矿执行器。候选身份和同地点目标证据均按目标逐项 fail-closed。
- Sap 当前版本重放已通过：`runtime-full-shipment-sap-refresh-20261005-183733` 完成采集、入箱、回家、原生睡眠与次日结算；睡眠为 `applied/verified`，日期 `1 -> 2`、`total_days 0 -> 1`、`active_menu=none`，recurrence checkpoint 为 `1` 次验证且剩余 `153`，rollout proof 独立重建通过。
- ShippingMenu 后台关闭不再移动系统光标。执行器使用 SMAPI `Escape` 按键覆盖并等待原生菜单关闭；本轮产物中旧错误 `shipping_summary_cursor_position_mismatch` 命中数为 `0`。
- 本轮还暴露并修正了部署漂移：此前远端源码/Release DLL 已更新，但 Mods 仍加载旧 Debug DLL。两个部署脚本现在对 `-NoBuild` 执行源码时间戳防陈旧检查，并对每个部署文件校验源/目标 SHA-256。定向守卫 `3/3`、Core game-free `212/212`；此前 Backend `225/225`、Bootstrap/Harness clean 继续有效。
- 防风草当时版本全量证明因低边际覆盖主动停止，已有产物保留但不得计入新证据。严格生产索引本轮不宣称 `3/26`；当时可独立验收的新版本原生证明为 Sap 与放射性矿石两层，正式训练仍为 `false`。
- 测试改为三层：动作/执行器改动只跑对应原生链路；共享代码跑离线全套和少量代表性原生回放；26 层生产索引只在分层新增、权威字典/日历变化或训练准入里程碑重建，不再随每次小改重复展开。
- 拓扑固定：源码仓库 `I:\StardewValleyAICompanion`，本机轻量/历史运行数据在 `E:`，高负载隔离游戏测试在异机 `F:\StardewAI-TestLab`。

## 历史记录 · 2026-10-05 Full Shipment 树苔藓样本复用原生镰刀链

- `(O)Moss` 已按唯一权威身份 `full_shipment:item:Moss / native_tree_moss_harvest / Tree.CreateMossItem / foraging.harvest_tree_moss` 接入共享样本 runner。
- proof 根外只用现有 `debug.setup_clear_obstacle` 建立成熟、无种子、带苔藓的 base `Tree` 和原生镰刀；正式 acquisition 继续复用既有透明投影、候选、DailyPlan、`executor.clear_obstacle`、原生 `Tree.performToolAction -> Tree.CreateMossItem` 与严格回执，没有新增第二套苔藓执行器。
- 严格 resume tuple 为 `full_shipment:item:Moss|(O)Moss|native_tree_moss_harvest`。`LZT` 样本守卫 `17/17`、Core game-free `138/138`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment receipt，生产索引仍为 `2/26`；当时已装配待运行 14 层、未装配 10 层。

## 历史记录 · 2026-10-05 Full Shipment 样本执行绑定按路线类型失败关闭

- 每个现有样本场景现在显式声明唯一预期 `route_kind`；生成 execution binding 后、进入任何游戏动作前，runner 会逐字段核对 `requirement_id / qualified_item_id / route_kind`。
- 同一需求或物品存在多条来源时，Teacher 即使选中了正确 requirement，只要来源层不符也会立即失败，不再把错误路线带入昂贵的原生运行。resume 端原有严格 tuple 校验继续保留，形成执行前与断点恢复双门。
- `LZT` 上样本守卫 `16/16`、Core game-free `137/137`、Bootstrap Release `0 warning / 0 error`。这是防误跑与节省测试资源的控制改进，不产生新的原生回执，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 太阳能板产物样本入口

- `native_solar_panel_output` 的唯一权威身份为 `full_shipment:item:787 / (O)787 / machine:(BC)231:OutputSolarPanel / farm.collect_machine_outputs`。
- proof 根外夹具锁定太阳能板机器 ID `231` 与电池组 `(O)787`，随后复用现有专用透明投影、共享机器候选、DailyPlan、编译器、`executor.collect_machine_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:787|(O)787|native_solar_panel_output`。
- `LZT` 上样本守卫 `15/15`、Core game-free `136/136`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 机器直接产物样本入口

- `machine_output` 的代表采用 `full_shipment:item:257 / (O)257 / farm.collect_machine_outputs`，权威来源固定为蘑菇树桩 `machine:(BC)128:rule:Default`。
- proof 根外夹具同时锁定机器 ID `128` 与输出 `(O)257`，随后复用现有机器投影、候选、DailyPlan、编译器、`executor.collect_machine_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:257|(O)257|machine_output`；没有把相邻的 `native_machine_item_query_output` 混入本层。
- `LZT` 上样本守卫 `14/14`、Core game-free `135/135`、Bootstrap Release `0 warning / 0 error`。本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 鱼塘产物样本复用原生收取链

- `native_fish_pond_output` 的代表采用鱼籽 `full_shipment:item:812 / (O)812 / fishing.service_fish_ponds`，夹具鱼种为鲟鱼 `(O)698`。权威清单包含鲟鱼产生 `(O)812` 的精确 route occurrence，因此没有沿用未被现有 smoke 精确覆盖的建议虚空蛋 `(O)305`。
- 样本在 proof 根外复用 `debug.setup_fish_pond_output`，随后走唯一的鱼塘透明投影、DailyPlan、编译器、`executor.collect_fish_pond_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:812|(O)812|native_fish_pond_output`。
- `LZT` 上样本守卫 `13/13`、Core game-free `134/134`、Bootstrap Release `0 warning / 0 error`。历史鱼塘 output smoke 只证明链可复用；本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 高级动物产物样本入口

- `native_farm_animal_deluxe_produce` 的代表采用大瓶牛奶 `full_shipment:item:186 / (O)186 / farm.collect_animal_products`，权威清单中精确来源为 Brown Cow 与 White Cow。
- 样本与普通牛奶共用 `debug.setup_animal_product_target` 配置表、Milk Pail、唯一候选/编译/执行/verifier 链；严格 resume tuple 为 `full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce`，没有新增高级产物执行器。
- `LZT` 上样本守卫 `12/12`、Core game-free `133/133`、Bootstrap Release `0 warning / 0 error`。现有 EVD-222 证明 Milk Pail 链可复用，但不构成 `(O)186` 的 Full Shipment 原生回执；生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 基础动物产物样本复用挤奶链

- `native_farm_animal_produce` 的运行时分层代表采用牛奶 `full_shipment:item:184 / (O)184 / farm.collect_animal_products`。该 requirement 在权威清单中精确绑定 Brown Cow 与 White Cow，避免把建议鸡蛋 `(O)176` 错配到只验证过挤奶/剪毛的夹具。
- 样本在 proof 根外使用既有 `debug.setup_animal_product_target` 建立 `White Cow / Milk Pail / (O)184 / quality 2 / cracker x1`，随后复用唯一的候选、DailyPlan、编译器、`executor.collect_animal_product` 和 fresh verifier。严格 resume tuple 为 `full_shipment:item:184|(O)184|native_farm_animal_produce`。
- `LZT` 上样本守卫 `11/11`、Core game-free `132/132`、Bootstrap Release `0 warning / 0 error`。EVD-222 的精确牛奶回执只证明链可复用；本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 果树产物样本复用原生果树链

- `native_fruit_tree_produce` 的运行时分层代表采用樱桃 `full_shipment:item:638 / (O)638 / foraging.harvest_fruit_tree`。它与清单中的其他果树产物属于同一分层；选择樱桃是因为现有 `fruit_tree / single_normal` 原生夹具和 action smoke 已精确覆盖该物品，不需要为苹果另造执行路径。
- 共享 forage 场景表新增 `fruit_tree / single_normal / Farm 64,15`，继续复用既有果树候选、DailyPlan、编译器、执行器和 verifier。严格 resume tuple 为 `full_shipment:item:638|(O)638|native_fruit_tree_produce`，未知组合仍失败关闭。
- `LZT` 上样本守卫 `10/10`、Core game-free `131/131`、Bootstrap Release `0 warning / 0 error`。历史果树 smoke 只证明原生链可复用；尚无本层 fresh Full Shipment acquisition proof，生产索引继续为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 地点觅食物样本入口

- `native_location_forage_spawn` 的生产代表为 `full_shipment:item:16 / (O)16 / foraging.collect_spawned_objects`。现有 EVD-211 链覆盖透明 spawned-object 投影、DailyPlan、`executor.collect_spawned_object` 与原生拾取回执。
- 共享 forage 场景表新增 `spawned_object / ordinary / Forest`，请求 `debug.setup_forage_source_fixture` 在 proof 根外建立 `(O)16`；实际合法格由 fixture 验证并通过 fresh snapshot 进入候选，不把请求坐标冒充最终坐标。
- 严格 resume tuple 为 `full_shipment:item:16|(O)16|native_location_forage_spawn`。`LZT` 上样本守卫 `9/9`、Core game-free `130/130`、Bootstrap Release `0 warning / 0 error`。
- 尚无本层 fresh Full Shipment acquisition proof，生产索引继续为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 春葱样本复用作物采集链

- 权威路线固定为 `full_shipment:item:399 / (O)399 / native_spring_onion_harvest / Crop.harvest / whichForageCrop 1 => (O)399 / foraging.harvest_spring_onions`。既有 EVD-335 原生 DailyPlan smoke 验证 `executor.harvest_crop`、背包增量、作物移除和 Foraging XP `+3`。
- 防风草与春葱现在由同一 crop 场景表驱动 `debug.setup_harvest_crop_target`：防风草使用 `seed_id=472`，春葱使用 `rule_key=spring_onion`。fixture 仍在 proof 根外，没有新增作物执行器。
- 严格 resume tuple 为 `full_shipment:item:399|(O)399|native_spring_onion_harvest`。`LZT` 上样本守卫 `8/8`、Core game-free `129/129`、Bootstrap Release `0 warning / 0 error`。
- 历史 EVD-335 只证明执行链可复用；新的 Full Shipment acquisition proof 待采集，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 岛屿棕榈样本精确地点根

- `native_wild_tree_seed` 的代表路线为 `full_shipment:item:88 / (O)88 / wild_tree:6 / foraging.harvest_tree_product`。匹配的 `island_palm` 原生 smoke 位于 `IslandSouth 20,20`，不能借用 Farm 根。
- 共享 forage 场景配置现显式携带 `LocationId / TargetTileX / TargetTileY`。现有样本继续绑定 `Farm 64,15`，岛屿棕榈单独绑定 `IslandSouth 20,20`；acquisition 根必须等于场景声明的精确地点。完整 Sap recurrence 仍要求 Farm 且 `total_day=0`，没有被放宽。
- 严格 resume tuple 为 `full_shipment:item:88|(O)88|native_wild_tree_seed`。`LZT` 上样本守卫 `7/7`、Core game-free `128/128`、Bootstrap Release `0 warning / 0 error`。
- 这只完成跨地点样本入口。新 Full Shipment 原生回执尚未产生，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 野树落种样本入口

- `native_wild_tree_seed_drop` 的生产代表为 `full_shipment:item:408 / (O)408 / wild_tree:2:0 / foraging.harvest_tree_product`。已有 `fall_hazelnut` 原生 smoke 在 Farm 精确产出 `(O)408`，验证原生 `checkAction`、种子消耗、完整输出域、零 Foraging XP 与空槽恢复。
- Full Shipment runner 现通过共享 forage 场景表复用 `wild_tree / fall_hazelnut` fixture 和既有树产品候选、编译器、执行器、verifier；resume 只接受精确 tuple `full_shipment:item:408|(O)408|native_wild_tree_seed_drop`。
- `LZT` 上样本守卫 `6/6`、Core game-free `127/127`、Bootstrap Release `0 warning / 0 error`。现有 action smoke 仍不替代 fresh Full Shipment acquisition rollout proof，生产索引保持 `2/26`。
- 岛屿棕榈 `(O)88` 虽有匹配 smoke，但其证明根必须位于 `IslandSouth`，本轮没有放宽 runner 的 Farm 根合同；该层留作独立跨地点切片。

## 历史记录 · 2026-10-05 Full Shipment 茶树样本复用既有灌木链

- 生产清单将茶叶 `(O)815` 精确绑定为 `full_shipment:item:815 / native_tea_bush_harvest / Bush.GetShakeOffItem / foraging.harvest_bushes`。已有 `tea_leaf` 原生 action smoke 验证输出 `(O)815`、原生 `checkAction`、灌木偏移/产物增量和 XP/追踪器合同。
- Full Shipment runner 新增 `tea_bush_harvest_sample`，复用 `debug.setup_forage_source_fixture` 的 `bush / tea_leaf` 配置及既有灌木候选、DailyPlan、编译器、执行器和 verifier。浆果、姜、茶叶的 fixture 参数现由同一场景配置表生成，避免继续扩展二选一分支。
- `LZT` 上专项守卫 `5/5`、Core game-free `126/126`、GoalConditionedBootstrap Release `0 warning / 0 error`。首次远端命令因漏传仓库规定的 `GameFreeGovernance=true` 而在 ModBuildConfig 的 GamePath 检查前停止，恢复正确参数后全部通过；这不是代码测试失败。
- 历史茶树 action smoke 不等于当时 Full Shipment acquisition rollout proof。浆果、姜、茶叶均待 fresh 原生样本和独立 proof，生产索引继续为 `2/26`，正式训练仍禁用。

## 历史记录 · 2026-10-05 Full Shipment 姜采集样本入口与异机验证

- 权威需求构建器与 lowering 已锁定姜 `(O)829` 为 `full_shipment:item:829 / native_ginger_harvest / foraging.harvest_ginger`，来源语义为 `Crop.hitWithHoe` 的 `whichForageCrop 2 => (O)829`。样本复用既有 `debug.setup_forage_source_fixture`、候选、DailyPlan、动作编译器和原生姜采集执行链，没有新增第二套执行器。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 新增 `ginger_harvest_sample`，与浆果样本共用 forage fixture 编排，但分别绑定 `dry_standard` 与 `berry_standard`；`Resume-RuntimeFullShipmentAcquisitionProof.ps1` 只接受精确的 requirement、物品与 route kind 三元组，未知组合仍失败关闭。
- 为避免本机再次因测试负载卡死，本轮较大验证转移到 `LZT` 隔离测试目录。远端 GoalConditionedBootstrap Release 构建为 `0 warning / 0 error`，acquisition route dispatch 和 Full Shipment 静态可编译性自测通过，Core game-free `125/125` 通过；本机只执行轻量解析、差异与 Git 检查。
- 这些结果证明样本入口、映射和离线合同没有回退，不是原生游戏运行证据。新电脑当时没有游戏运行环境，因此浆果和姜均仍待 fresh 原生执行、终态核验及独立 rollout proof；生产索引继续严格保持 `2/26`，`formal_product_training_authorized=false`。
- 下一固定动作：继续为剩余分层准备可复用样本入口；当具备可交互游戏环境时，优先运行浆果，再运行姜。只有每条原生回执被生产 evidence index 接纳后才能逐层增加覆盖数。

## 历史记录 · 2026-10-04 Full Shipment 灌木采集样本已就绪、待原生运行

- 权威 requirement/lowering 已复核 `(O)296`：`full_shipment:item:296`、`native_bush_shake`、`foraging.harvest_bushes`，与已有 `executor.harvest_bush` 原语及 `berry_standard` 原生夹具一致；没有新增候选、编译器或执行器。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 现支持 `berry_bush_harvest_sample`，按夹具设置后的真实 `total_days` 构建目标日期；仅完整 Sap recurrence 继续强制第 0 天根，因此样本适配没有放宽递归证明。
- `Resume-RuntimeFullShipmentAcquisitionProof.ps1` 不再写死防风草摘要，而是从已验证 execution binding 读取 requirement、物品和 route kind，并对当前两种样本严格映射。未知组合失败关闭。旧防风草证据已离线完整重放通过。
- PowerShell 解析、GoalConditionedBootstrap Release、acquisition route dispatch、自包含 Full Shipment 静态可编译性及 Core game-free `124/124` 均通过。当时系统没有可用的交互桌面会话，MonoGame 原生运行没有启动；因此生产索引仍严格保持 `2/26`，不得提前声称 `3/26`。
- 下一固定动作：出现交互桌面会话后运行一次浆果灌木样本；若原生执行、fresh 终态和最终 rollout proof 全部通过，再登记 `full_shipment.runtime_sample.04.native_bush_shake` 并重建严格索引为预期 `3/26`。

## 历史记录 · 2026-10-04 审计维护：Issue 与文档状态清理

- 新建 #162 跟踪 `scripts/Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 的维护性拆分。当前脚本约 1.4k 行，同时承担进程生命周期、快照、规划轴、Teacher、rollout proof、Sap 出货、恢复/睡眠和 recurrence checkpoint 编排。该项定级为 P2 维护债，不阻塞当前证据工作；拆分必须复用既有 planner/compiler/executor/verifier，不得形成第二套运行链。
- 关闭历史 P0 #7。当前 TransparentBridge 已移除 `ApplyAiControlSettings` 与直接 `Game1.options.*` 写路径；Capability Manifest 从真实 Adapter 与二进制身份构建；event.v2 使用 `observed_snapshot_hash / published_snapshot_hash / snapshot_relation`；Bridge 继续 `CanExecuteCommands=false / CanWriteGameState=false`。
- #6、#8、#12 经 fresh audit 后继续保持开放。#6 的仓库级 E3/E4 Evidence Index/Build Manifest/Action Capability Matrix 尚未全部闭合；#8 的完整 locked restore + TRX + authoritative materialization + per-commit CI 仍未确认完成；#12 虽已有 material/currency reservation、partial consume、relocation、machine-support intent 等实现，但通用 commitment union、CoopOwnership 与 Interaction Lease 仍未完整闭合。
- `FORMAL_FULL_TRAINING_READINESS_CN.md` 已补 2026-10-04 当时口径：Full Shipment 保留 154/154 静态全覆盖和完整 recurrence 的游戏/可选验收能力，但训练运行时证据采用 26 个真实执行层的分层抽样；历史 `0/154` 与“154 recurrence 是训练前置”的描述仅作历史记录。
- 本轮没有发现需要新增 P0/P1 的代码质量回退；当前最明确的新维护债仍是 #162。

<!-- BEGIN GENERATED CURRENT CHECKPOINT -->
## Machine-generated current checkpoint

- Source commit: `4141faf511bd07879b83ec36cd1448b57e42ddc7` (generation input; must be an ancestor of current HEAD)
- Latest evidence: `EVD-338`; generated at: `2026-10-28T22:48:57.6909601+00:00`
- Catalog: `242 registered / 244 semantic / 241 compiler-bound / 163 five-gate / 67 training-allowlist`
- Execution: `154 product-executor / 2 catalogued-blocked`
- Native evidence: `322 surfaces / 448 branches / 150 map tokens`; fingerprint: `a2ba3ac0193fa78aece39a1eb4c18cb3c9b3248ea7390194dcfe61605af02d0e`
- Planning catalog: `planning_semantic_catalog_complete`; `stardewai.planning_semantic_catalog_fingerprint.v1`; fingerprint: `ef0335aa0fca8744c3f61bee7e2f32075857c3c60eef32da1057bb21e699da5a`
<!-- END GENERATED CURRENT CHECKPOINT -->

## 历史记录 · 2026-10-04 Full Shipment 普通作物运行时样本闭合

- Stardew Valley 1.6.15 反编译复核确认：普通作物的来源种子身份来自 `Crop.netSeedIndex`，`Crop.whichForageCrop` 只适用于野生种子分支。透明桥已按该边界修正，普通防风草实时候选现在发布 `harvest_source_seed_id=472`，不再因来源字段为空被 acquisition dispatch 拒绝。
- 隔离运行 `runtime-full-shipment-parsnip-sample-20261004-135111` 已沿唯一现有链完成 `farm.maintain_crops -> executor.harvest_crop`：严格 dispatch 成功，原生回执为 `applied/verified`，作物从成熟变为已移除，背包增加 `(O)24 x1`，Farming XP `0 -> 8`。随后结算、checkpoint 与最终独立 rollout proof 全部通过。
- 生产抽样索引现为 `2/26`：`harvests_as` 由防风草证明，`native_wild_tree_chop_drop` 继续由 Sap 证明；共享出货/睡眠 recurrence 仍通过，剩余 24 层，`formal_product_training_authorized=false`。这不是 `2/154` 进度，也不得退回逐物品重复执行。
- 中间结算新增 verified-artifact 快路径：复用前必须逐项校验 schema、状态、路线/候选身份和全部源文件 SHA-256；最终 rollout proof 仍执行一次完整独立重建。严格/快路径 JSON 等价回归通过。新增续证脚本只从已完成的原生执行产物接续结算与证明，不重启游戏或重做动作。
- 验证：GoalConditionedBootstrap 与 Backend Release 构建均为 `0 warning / 0 error`，`self-test-acquisition-route-dispatch` 通过，普通作物来源守卫 `1/1` 通过，严格生产索引发布 `verified=2 / missing=24`。下一固定切片是盘点剩余 24 层中可复用的既有原生 smoke，并逐层生成同一 acquisition rollout 合同；不得新增第二套候选、编译器或执行器。

## 历史记录 · 2026-10-03 Full Shipment 抽样证据索引基础设施

- 新增 `build-full-shipment-runtime-sample-evidence-index`。它以 `full_shipment_static_compilability_inventory.v2` 的分层结果为唯一分母，重新构建每个 acquisition rollout proof，并精确匹配 `route_occurrence_id`、权威 requirement/lowering 哈希和所属路径族；只读 action smoke、人工声明和同一层内重复物品均不能增加覆盖数。
- 出货、睡眠结算和 Full Shipment 进度增量是 26 个 acquisition 路径族共享的尾链，只要求一条经 `FullShipmentRecurrenceProofBuilder` 重新计算且与落盘 checkpoint 完全相等的真实 recurrence prefix。每个路径族则只要求一条完整 acquisition proof，不要求再为该层其他物品重复出货。
- 索引显式区分 `partial_runtime_sample_evidence` 与 `verified_complete_runtime_sample_evidence`，发布已验证数、缺失数和精确缺失层 ID；重复层、重复 route occurrence、陈旧 receipt、错误 authority 哈希、错误路径族及不完整 rollout 均 fail-closed。完整 154 项 recurrence 继续仅用于正常游戏和可选最终验收。
- Release 构建为 `0 warning / 0 error`；新索引的部分/完整/缺共享尾链/重复样本回归和原静态分层回归均通过。当时尚未把普通历史 smoke 冒充正式样本，Goal Method 生产覆盖仍为 `4/19`；当时后续计划只建立生产静态清单与证据 manifest，先登记 Sap 的精确 proof，再列出真正缺失的路径族。

## 历史记录 · 2026-10-03 Full Shipment 验证改为分层抽样

- `154/154` 仍是权威静态分母和 AI 实际游玩时必须完成的游戏目标，但不再是开发验证或正式训练准入所需的 154 次重复原生运行。此前 `1/154` 只保留为首轮端到端诊断证据，不作为要求继续逐项跑到 154 的项目进度条。
- 生产 lowering 中的 `641` 条来源和 `154` 个物品归并为 `26` 个“路线族 + 执行签名”运行时抽样层。每层只需一条精确绑定来源、候选、编译队列、原生回执和 fresh 前后快照的代表证据；同层其余物品由 `154/154` 静态来源身份、参数编译和运行时绑定审计覆盖。已有合格原生 smoke 可以复用，不要求重新从新档重复采集。
- 完整 `154/154` fresh-save recurrence 构建器继续保留，用于 AI 正常游玩和最终可选的整局成就验收，不再阻塞 Full Shipment Teacher corpus、`4/19 -> 5/19` 或正式训练准入。最终 achievement 34 的 `153/154 -> 154/154` 原生边界仍由现有独立终态 smoke 验证。
- 当前机器可读清单升级为 `full_shipment_static_compilability_inventory.v2`，显式发布抽样层、`full_recurrence_required_for_training=false` 和 `full_recurrence_retained_for_acceptance=true`。当时后续计划是建立抽样证据索引，先复用已有真实回执，再只补没有代表证据的路线族；不得继续机械执行剩余 153 个物品。

## 历史记录 · 2026-10-03 Full Shipment 真实 fresh-save 首轮前缀闭合

- 隔离原生新档 `ProofFarm_450250338` 已完成第一条真实递归：从 Spring 1 的 `0/154` 出发，复用既有 `foraging.chop_wild_tree` 路线原生砍树、拾取 Sap、投入出货箱、回家并原生睡眠结算。没有 fixture、传送、直接改档或第二套动作执行器。
- 野树掉落采用 typed deferred-pickup 合同：编译时绑定树格、权威来源、保证掉落、执行前库存/地面物基线；运行时砍树后只允许在同队列内按 fresh state 重绑定拾取目标。队列收据校验同时覆盖普通连续步骤与显式 Teacher state rebind，未声明的状态重绑定继续 fail-closed。
- 出货阶段真实运行时，角色走向出货箱途中磁吸到第二个 Sap。Teacher 验证现在只在最后一个精确 `executor.ship_inventory_item_to_bin` 步骤同时证明库存 `2 -> 1`、出货箱 `0 -> 1`、队列最终哈希一致时接受该情况；整队列净库存不再被误当作唯一出货证据，其他库存或箱体漂移仍拒绝。
- 恢复链暴露了透明桥哈希边界问题：同一 `game_tick=1181` 的两份等价快照仅有 `machine_probe_cache_tick: 1179 -> 1181` 不同。该字段是采样缓存元数据而非游戏语义状态，现不再参与新状态哈希；后端兼容验证旧哈希。旧证据只有在两侧嵌入哈希各自可验证且规范化状态完全一致时才允许连接，真实库存、日期、地点或机器状态变化仍会断链。
- 离线全轴证明已生成 `full_shipment_recurrence_prefix_checkpoint.v1`：`prefix_proof_verified=true`、`verified_iteration_count=1`、`remaining_item_count=153`、`final_total_day=1`、`ready_for_next_iteration=true`、`complete=false`、`achievement_34_verified=false`。这是首个真实 `1/154` 检查点，不是完整 Full Shipment 训练准入；`formal_training_authorized=false` 保持不变。
- 下一固定切片从该检查点的 final snapshot 作为唯一递归根，选择下一个权威 acquisition endpoint，继续复用现有候选、编译、原生执行、出货和睡眠链。不得重写已完成动作，也不得把单轮前缀冒充 154 项完整证明。全轴前缀验证目前约需 12 分钟，后续可缓存不变的权威轴输入，但优化不得削弱证据重算与哈希绑定。

## 历史记录 · 2026-09-28 Full Shipment 跨图结算证明修复

- 审计发现 recurrence 证明错误地要求“投入出货箱后的快照”与“最终睡眠前快照”哈希完全相同，但现有 `recovery.stabilize_day` 按设计每次只执行一个原生连接点并刷新快照。角色从农场出货箱走回农舍必然改变状态，因此旧合同实际上只能依赖传送或调试夹具，不能承载真实新档证据。
- settlement manifest 现可记录最多 8 段有序 `recovery_transitions`。每段仍复用现有 recovery 候选、DailyPlan、动作编译器和产品执行器，只允许已知的跨连接点、关闭阻塞菜单或短等待原语；中间段禁止睡眠。队列、前后快照和原生执行回执都经共享校验器重建验证，并强制同一存档/玩家、哈希连续、日期不变、Full Shipment 进度不变以及本轮物品仍精确留在出货箱中。
- prefix/whole-recurrence 的每轮 evidence 会固化每段恢复队列、前后快照和执行回执的 SHA-256；重复文件、断链、乱序、跨角色、提前跨日、出货进度漂移和待结算物品消失均失败关闭。链尾仍必须接原有 ordinary/terminal 原生睡眠回执，没有新增第二套路由、恢复、睡眠或出货实现。
- 专项 Release 构建为 `0 warning / 0 error`，Full Shipment settlement 自测覆盖正常恢复及睡眠混入、日期/进度/出货箱/角色/顺序篡改拒绝。该修复只解除首轮真实 recurrence 的证明层硬阻塞，生产进度仍是 `0/154`，`formal_product_training_authorized=false`；当时后续计划仍是从已验证原生 fresh save 采集 Sap 的第一条真实 `1/154` 前缀。

## 历史记录 · 2026-09-28 原生 fresh-save 运行根已闭合

- `RuntimeTestHarness` 新增严格隔离的 `CreateFreshSave` 模式。实现先按当前 1.6.15 反编译结果调用 `Game1.resetPlayer()`，设置确定性的标准农场角色配置，再调用原生 `TitleMenu.createdNewCharacter(skipIntro: true)`；不构造或改写存档 XML，也不复制历史存档充当新档。
- fresh-save 模式必须同时满足 `STARDEWAI_TRAINING_MODE=1`、显式且相同的 `STARDEWAI_TEST_SAVES`/`STARDEWAI_SAVE_ISOLATION_PATH`、空的 `fresh-save-*` 专用目录、非盘符根和非重解析点，并且不能指定已有 slot。任一条件不满足时整个 harness fail-closed，执行器也不会启动。
- 新增隐藏静音 `Invoke-RuntimeFreshSaveSmoke.ps1`。真实运行 `runtime-fresh-save-20260928-133156` 已在游戏 1.6.15 中原生创建 `ProofFarm_450250338`，透明桥读到 Spring 1、Full Shipment `0/154`、无 achievement 34，并等到主存档和 `SaveGameInfo` 都落盘后才通过。第一次运行暴露并修复了健康检查硬编码 60 秒、忽略脚本 180 秒启动预算的问题；失败证据保留，未覆盖旧存档。
- 这只闭合“可复现的新存档根”，尚未闭合任何 Full Shipment acquisition/deposit/settlement 迭代，因此生产 recurrence 仍是 `0/154`，`formal_product_training_authorized=false`。下一切片从该原生根执行可审计的出屋连接动作，再用现有 `foraging.chop_wild_tree` 获取 Sap、原生投入出货箱并跨日结算，最后交给 prefix checkpoint 验证为第一条 `1/154` 证据；不得增加第二套移动、砍树或出货实现。

## 历史记录 · 2026-09-28 Full Shipment 真实 recurrence 增量检查点

- 新增 `build-full-shipment-recurrence-prefix-checkpoint`，复用既有 whole-recurrence 的 acquisition rollout、`economy.ship_items` Teacher receipt、ordinary/terminal sleep settlement 验证核心，使真实证据可以按 `1..154` 轮逐步落盘，而不必等 154 轮全部完成后才发现中间链路错误。
- prefix 仍必须从同一新存档的第 0 天、零出货状态开始；每轮 shipped count 必须连续 `n -> n+1`，前一轮 settlement after snapshot 必须是下一轮 acquisition 的精确根。未完成前缀不得出现 achievement 34、terminal settlement 或占用第 224 天，且 checkpoint 会发布精确的剩余 requirement/item 集合与最终快照哈希。
- 原 `build-full-shipment-recurrence-proof-receipt` 现在调用同一共享验证核心，仍严格要求全部 `154` 轮、最终 achievement 34 和完整分母；没有复制第二套 recurrence、候选、编译或执行系统。空前缀、提前终态、把前缀冒充完整序列、把完整序列冒充未完成前缀及超期非终态均有拒绝回归。
- 当时只完成增量验证基础设施，尚未取得真实 fresh-save 第一轮证据，因此生产 recurrence 进度仍是 `0/154`，`formal_product_training_authorized=false` 不变。当时后续计划是由隐藏静音原生运行生成第一轮真实 manifest/artifacts，再用该 checkpoint 验证并固定下一轮根；合成夹具不得计入进度。

## 历史记录 · 2026-09-28 Full Shipment 154 项静态可编译性清单完成

- 新增 `build-full-shipment-static-compilability-inventory`，从权威需求清单、acquisition lowering、动作实现 reconciliation 和五类 supporting-transition 终端谱系证据中重建 Full Shipment 的静态可编译性；它不信任调用方声明的汇总状态。
- 当时生产输入严格核对为 `154/154` 个必需物品组、`641` 条权威 acquisition route、`26` 种实际路由、`504` 个 `route_kind + source_id` 身份和 `24` 个被引用动作选项。全部来源身份契约、endpoint/supporting option 编译绑定、运行时绑定和支持链谱系通过，阻塞项为 `0`。
- 权威 acquisition 总目录的 `33` 种路由均有显式来源身份契约；未知路由种类、空/畸形来源、陈旧 lowering 哈希或摘要计数、缺失依赖轴、未绑定编译器、缺失运行时绑定和不完整支持谱系全部 fail-closed。没有引入第二套收集规划、编译器或执行器。
- `static_compilability_complete=true` 只证明：当时状态满足先决条件时，每个 Full Shipment 需求至少存在一条可由现有链路精确编译的路线。它不证明同一新存档已实际完成 154 项递归 acquisition/shipping 闭环；因此 `fresh_save_recurrence_evidence_complete=false`、`formal_product_training_authorized=false` 保持不变，Goal Method 生产覆盖仍为 `4/19`。
- 验证通过：Release 构建 `0 warning / 0 error`、新增静态清单正/负例、真实生产 154 项清单、`self-test-acquisition-route-dispatch`、`self-test-bootstrap-hermetic`、Core game-free `109/109` 以及完整 `self-test-current-stage-one-collection`。完整回归覆盖五类 supporting transition、三分区连续重规划及篡改拒绝，没有发现共享链路回退。
- 下一固定切片是建立第一条真实新存档 Full Shipment recurrence 证据链：从 fresh snapshot 选择权威需求，经现有 route/source/support 编译与原生执行，取得终态物品及 shipping credit，重新规划下一项，并把每一轮的哈希、队列、原生回执和剩余分母落盘。不得以合成 fixture 冒充生产证据，也不得重新开发已有的 154 项动作链。

## 历史记录 · 2026-09-28 Full Shipment 当时候选权威来源身份闭合

- 修复了 `CurrentFullShipmentTeacherFrontierBuilder` 的错误正标签边界：旧实现只要求当前候选产物精确、option 属于该物品的权威 endpoint，因而同一 option 下来自错误作物、商店或原生掉落源的候选仍可能被标成 Teacher 正例。现在 Full Shipment 前沿直接复用生产 acquisition dispatch 的唯一来源匹配器，同时绑定 `route_kind + source_id + qualified_item_id`；没有建立第二套来源解析或收集规划。
- acquisition 正例现在必须在该需求的 admitted endpoint routes 中恰好匹配一条权威来源。商店、作物种子来源、普通地点/矿井钓鱼、蟹笼、固定反编译原生来源和候选携带的 typed authoritative source JSON 均沿用实际调度规则；来源缺失、来源错误、来源歧义或多条 route 同时命中均失败关闭。原生 `economy.ship_items` 终态仍由精确 Full Shipment 贡献字段证明，不伪造获取来源。
- 同时修复共享参数读取器的歧义漏洞：两个同名同值参数过去会先经 `Distinct()` 折叠后被当成“唯一”，现在必须物理上恰好出现一次。Full Shipment 前沿与 acquisition dispatch 均加入重复同值、缺失、错误和歧义来源负例；Stage 1 共享防风草候选夹具补齐真实 `harvest_source_seed_id=472`，继续验证一次执行同时给 Full Shipment 和社区中心记 exact credit。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`、`self-test-acquisition-route-dispatch`、完整 `self-test-current-stage-one-collection`、`self-test-bootstrap-hermetic`、Core game-free `109/109` 及 `git diff --check`。首次未带 `GameFreeGovernance=true` 的 Core 命令被本机缺少 SMAPI 的旧游戏目录门禁拒绝，使用仓库 CI 标准 game-free 命令重跑后全部通过；该环境性拒绝不计为代码测试失败。
- 本切片没有增加生产 Teacher/native 三分区证据，Goal Method 覆盖仍为 `4/19`，聚合状态仍为 `blocked_incomplete_goal_method_teacher_coverage`，`formal_product_training_authorized=false`。下一固定切片是对 154 项 ordered recurrence 的实际 endpoint/source/supporting-option 可编译性做精确缺口盘点，再按现有 dispatch/compiler 路径闭合第一组确定性来源；不得回退到“同产物 + 同 option”弱匹配，也不得新增第二套执行链。

## 历史记录 · 2026-09-28 宠物爱心真实三分区生产证据完成

- `Invoke-RuntimePetCareSmoke.ps1` 新增只运行终端宠物爱心转移的 `TeacherTerminalOnly` 模式，并持久化 fresh before/after 快照及原生执行结果。快照保存改为直接写入桥返回的原始 JSON；不再把 PowerShell 对象重新序列化后冒充权威快照。Windows PowerShell 5.1 的快照 GET 显式使用 `-UseBasicParsing`，避免旧 IE 解析器造成空引用和假超时。
- 三个隔离存档分别产生真实 train、validation、test 证据：`runtime-pet-love-train-raw-20260928-0915`、`runtime-pet-love-validation-raw-20260928-0920`、`runtime-pet-love-test-raw-20260928-0925`。每条均由原生 `executor.pet_interact` 完成友情 `994 -> 1000`、当天抚摸次数递增和 `petLoveMessage false -> true`，且 fresh 前后状态哈希不同。
- 严格 `PetLoveTeacherCorpusBuilder` 已重新计算三组原始快照状态哈希并接受全部来源；分区由 `save_id + player_id + total_day` 派生，结果为 `ready_split_complete_pet_love_teacher_corpus`、`3 rows / 3 partitions`，Teacher 比较与原生终态验证全部为真。此前由 PowerShell 重序列化生成的无效快照未进入该 corpus。
- 生产 Goal Method 覆盖在保持同一权威字典、依赖展开、动作矩阵和 claim ledger 的前提下由 `3/19` 提升至 `4/19`。宠物爱心 criterion 现为 `executable_frontier`、train/validation/test Teacher 与原生结果分区齐全、`coverage_gate_ready=true`；全局仍为 `blocked_incomplete_goal_method_teacher_coverage:4/19`，`formal_product_training_authorized=false`。
- 验证通过：PowerShell AST、宠物运行源码守卫 `5/5`、Core game-free `109/109`、GoalConditionedBootstrap Release `0 warning / 0 error`、`self-test-bootstrap-hermetic`、三次隐藏静音 Teacher 原生运行，以及普通抚摸/满心礼物/隔夜水碗的完整旧路径 `3/3`。隔夜测试后已用克隆自身的 `_old` 文件恢复第 223 天，不触及原始游玩存档。GitHub 当时没有新增代码审查 issue；#130 的剩余夹具拆分已在上一切片完成并关闭。
- 下一固定切片是补齐 Full Shipment 当前实时 acquisition candidate 的权威来源身份和所需 supporting option 编译，使既有 154 项 ordered recurrence 能消费真实当前端点；不得复制第二套收集规划、执行或训练链。随后才采集该方法的 fresh-save 完整证据。

## 历史记录 · 2026-09-28 Stage 1 collection 自测夹具分解（#130）

- 将 `BootstrapSelfTest.StageOneCollectionFrontier.cs` 从约 4900 行的单方法拆为薄入口和 8 个同前缀 partial 文件，职责分别为夹具路径/调度、静态权威输入、目标日期轴、组合提交与执行、组合语义、负例与篡改拒绝、当时前沿准入、共享构建支持；单文件最大约 1350 行。
- 拆分只显式传递原方法已有的 `fish`、`stateHash`、货币路由与 reservation 依赖，原有 `BuildFacilityFixtureChain` / `BuildProcessingFixtureChain` 仍是唯一共享实现，没有复制第二套候选、编译、执行或训练链。既有 `BootstrapSelfTest.StageOneCollectionFrontier.Fixture.cs` 内容保持不变。
- 验证通过：GoalConditionedBootstrap Release 构建 `0 warning / 0 error`、完整 `self-test-current-stage-one-collection`、`self-test-bootstrap-hermetic`、Core game-free `104/104`，且 `git diff --check` 通过。本切片不改变任何业务输出、训练准入或 `formal_product_training_authorized=false` 状态。

## 历史记录 · 2026-09-28 Skull Key 真实三分区生产证据完成

- `build-skull-key-teacher-corpus` 现已重放三条真实 `plan_execution_episode.v1`：既有 EVD-106 作为 train（第 209 天），本轮隔离运行 `runtime-skull-key-validation-20260928-062858` 作为 validation（第 223 天），`runtime-skull-key-test-20260928-064032` 作为 test（第 223 天）。三个分区均由 `save_id + player_id + total_day` 哈希派生，调用方不能指定分区。
- validation 隐藏静音运行通过 `32/32` 个原生步骤：119 层清障、下梯、120 层移动、`claim_skull_key` 的 `false -> true` 转移以及退出矿井全部验证；test 运行通过 `5/5` 个步骤，并在第 4 步完成相同终态。三条 corpus 行的 Teacher 对比与原生终态验证均为真，状态为 `ready_split_complete_skull_key_teacher_corpus`。
- 真实采集暴露并修复两个运行缺陷：无楼梯物品是已知业务阻塞，透明桥现发布 `blocked_no_staircase_inventory`，不再把整组矿井数据误判为不可读；矿井快照脚本的执行端健康检查现复用 `StartupTimeoutSeconds`，不再用 30 秒硬编码误杀正常冷启动。两项均有定向回归测试。
- 真实 EVD 还确认历史 episode 使用高层 `mining.obtain_skull_key` 队列项，并在 `normalized_command` 内下沉到 `executor.interact`；旧快照缺少 `time.total_days` 且使用历史字段 `skull_key_special_item_which`。适配器只在严格日历换算和唯一旧字段别名下兼容历史证据，冲突字段、冲突重复参数或队列漂移继续失败关闭。
- 生产 Goal Method 覆盖现为 `3/19`，Skull Key 的 Teacher 与原生终态分区均为 `train/validation/test`，该 criterion 已 `coverage_gate_ready=true`。全局仍为 `blocked_incomplete_goal_method_teacher_coverage`，`formal_product_training_authorized=false`；不得把单项闭环写成全量训练准入。下一固定切片是连接宠物爱心的真实三分区原生证据，然后再处理剩余依赖图，而不是继续扩写第二套矿井/箱子执行系统。

## 历史记录 · 2026-09-28 Skull Key Teacher 源适配与真实桥接契约修正

- 新增 `build-skull-key-teacher-corpus`。它不接受裸执行结果作为监督，而是从 `plan_execution_episode.v1` 重放并绑定 fresh before/after snapshot、有效队列项和原生 `training_execution_result.v1`。只有同一存档、玩家、版本和 Stage-1 日期内，普通矿井 120 层唯一原生 Skull Key 奖励箱从存在到消失、`player.has_skull_key` 从 `false` 到 `true`，且原生开箱、领奖和状态转移原因全部成立时，才生成 Teacher 行。
- 审查真实透明桥时发现字段漂移：桥原先发布 `skull_key_special_item_which`，编译器与既有快照契约读取 `special_item_which`。现已统一为后者，并增加 game-free 源守卫；否则合成夹具会通过，而真实 120 层快照永远无法编译领奖动作。
- corpus 会从 `save_id + player_id + total_day` 自行派生 train/validation/test，不接受调用方伪造分区。验证器还精确绑定 `queue_item_id`、`requested_effect`、`observed_effect`、changed facts、目标格、overlay object、`SkullKeyChest` 和 `player.has_skull_key=true` 后置条件。篡改 corpus 行、episode 队列项或 effect 均失败关闭。
- 定向三分区夹具使 Skull Key criterion 精确进入准入，覆盖报告从 `2/19` 变为 `3/19`；但该夹具不是生产证据。生产准入总门仍为 `2/19` criterion、`1/11` root method，Teacher 阻塞现为 `dependency_graph_incomplete:15`、`evidence_not_connected:2`，不再存在 `teacher_source_adapter_missing`。
- Release 构建零警告，Core game-free `104/104`、bootstrap hermetic、定向 Goal Method 覆盖/篡改回归与正式准入总门重算通过。`formal_product_training_authorized=false` 和 `ready_for_separate_promotion_review=false` 保持不变。当时后续计划不是再写 Skull Key 执行器或适配器，而是从三个独立存档/玩家/日期分区采集真实 120 层原生回执并接入生产 coverage request；未取得真实回执前不得把 `3/19` 夹具结果写成当时进度。

## 历史记录 · 2026-09-28 正式训练准入总门重算

- 新增只读命令 `build-goal-method-formal-training-admission-reconciliation`。它从原始 frontier 输入重建 19 项 Grandpa 21 分 criterion，重放 acquisition corpus 及其 rollout/admission 来源，核验 train/validation/test 分区，重新绑定 checkpoint 并用其权重重算训练/验证/测试指标，同时重建 5 类支持动作的终端谱系矩阵；旧汇总 JSON 不能自行证明通过。
- 当时可信基线是 `2/19` criterion、`1/11` root method、`3` 个来源/`9` 行 corpus，train/validation/test 各 `3` 行、`2` 个 pair，validation/test pair accuracy 均为 `1.0`，支持谱系 `5/5`。这只证明现有小语料和证据链内部一致，不代表样本规模充分，也不代表正式训练已经准入。
- 九项总门中，权威分母、corpus 重放、split 完整性、checkpoint/corpus 绑定、留出集指标重算、支持谱系和叶制品权限隔离通过；完整 Teacher 覆盖与引用动作执行库存仍失败。Skull Key typed adapter 完成后的精确 Teacher 缺口为 `dependency_graph_incomplete:15`、`evidence_not_connected:2`。
- 下游真实动作缺口缩到两组：三个清障 executor 缺透明读取、原生运行与 five-gate 证据；三个社交动作缺内部执行管线。高层策略动作只要能经既有 `internal_execution_pipeline` 下沉，就不再因没有直接 Product Executor 被误报为阻塞。
- pairwise ranker 的两个 checkpoint 加载入口现都会从绑定 corpus 和模型权重重算训练/验证/测试摘要；结构合法但伪造 `test_pair_accuracy` 的 checkpoint 会失败关闭。Release 构建、bootstrap hermetic 以及全新输出根上的完整 `self-test-current-stage-one-collection` 均通过。
- `formal_product_training_authorized=false`、`ready_for_separate_promotion_review=false` 保持不变。`obtain_skull_key` 与宠物爱心现在都已有 typed adapter，但都仍需生产来源接线和真实 train/validation/test 原生回执；不得用 hermetic fixture 冒充。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 5/5

- `crop_planting` 现已复用既有 `farm.maintain_crops -> executor.plant_seed` 候选、编译和原生状态回执，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套农田规划、动作编译、寻路、ledger 或执行器。
- 证明逐项核验防风草种子 `(O)472` 从 `3 -> 2`、目标地块作物从不存在变为唯一 `(O)24` 防风草、作物未死亡且尚未成熟，并完成唯一种子 material reservation。种植不会伪装成即时收获；fresh Teacher 在同一 `full_shipment:item:24` 需求内改选确定性商店终态备选，种植 run ID 仍从终态 native outcome 中排除。
- 严格覆盖矩阵现为 `5/5`：`crop_planting`、`machine_input_load`、`machine_input_material_transfer`、`machine_input_purchase`、`machine_capacity_establishment` 均恰好有一条可重算证据，全部 `coverage_verified=true` 且 `support_excluded_from_terminal_outcomes=true`；缺失、重复或支持动作泄漏为终态标签仍失败关闭。
- Release 构建、定向 crop planting 谱系、三条受共享夹具影响的机器支持链、acquisition dispatch、bootstrap hermetic 与全新输出根上的完整 `self-test-current-stage-one-collection` 均通过。全量报告状态为 `verified_complete_support_terminal_lineage_coverage`，无 missing/blocking 项。
- `formal_product_training_authorized=false` 保持不变。下一固定切片转入正式训练准入总门审计：从现有 corpus、split、rollout、admission 和 holdout 制品中重新计算剩余阻塞，区分预期的产品安全锁与真实缺口；不得因支持矩阵完成而直接放开训练。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 4/5

- `machine_input_purchase` 现已复用既有权威商店报价、`economy.buy_supplies -> executor.buy_shop_item -> executor.close_menu`、原生购买回执与 currency reservation 结算，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套商店、购买、寻路、ledger、终端编译器或执行器。
- 证明绑定唯一 `FixtureShop / fixture-wheat-seed / (O)262 / 80g / 1` 库存行，逐项核验金币 `1000 -> 920`、物品 `0 -> 1`、菜单安全关闭以及唯一货币 reservation `80 -> 0`。支持动作 run ID 继续从终端 native outcome 中排除。
- 该链暴露并修正了生产制品验证器中的契约矛盾：精确机器输入购买本来就以 `resolved_resource_input_miss` 进入 typed purchase prerequisite，旧验证却要求所有资源已经满足。现在只有通过既有 `ReservationMaterialInputs` 精确绑定校验的购买缺口可被接受；无购买前置或绑定不精确的资源缺口仍失败关闭。夹具同时补齐 `sleep_prompt_context`，没有跳过 `close_menu` 的 required-state 校验。
- Release 构建、定向 purchase 谱系测试、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。矩阵现为 `4/5`，唯一缺口是 `crop_planting`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用既有种子获取、农田位置方案、整地/浇水/播种队列、原生作物状态回执和材料 reservation 结算，建立 `crop_planting` 的同型 file-backed 终端谱系证明；不得新增第二套农田规划、动作编译、寻路、ledger 或终端执行实现。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 3/5

- `machine_input_material_transfer` 现已复用既有 `inventory.transfer_item -> executor.move_to_tile -> executor.transfer_material`、原生搬运回执与 reservation relocation 结算，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套搬运、寻路、ledger、终端编译器或执行器。
- 证明夹具从当前地图普通箱子精确搬运一份 `(O)262` 到玩家背包，逐节点、逐槽核验源数量 `1 -> 0`、目标数量 `0 -> 1`，并要求唯一活动 reservation 保持数量不变且迁移到实际观测到的背包槽。支持动作 run ID 仍必须从终端 native outcome 中排除。
- 该链暴露并修正了测试权威清单未列出真实支持选项 `inventory.transfer_item` 的问题；同时把新增搬运快照夹具拆到独立 partial 文件，避免继续扩张机器容量快照文件。生产候选、编译和执行路径均直接复用，没有按测试特例放宽准入。
- Release 构建、定向 material-transfer 谱系测试、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。当时矩阵为 `3/5`，尚缺 `crop_planting` 与 `machine_input_purchase`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用已经闭合的权威商店报价、开放窗口、`economy.buy_supplies`、原生购买回执和货币 reservation 结算，建立 `machine_input_purchase` 的同型 file-backed 终端谱系证明；不得新增第二套商店、购买、货币 ledger、寻路或终端执行实现。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 2/5

- 新增严格的 file-backed 覆盖矩阵。支持类型只能从已结算支持请求的制品中推导，调用方不能自行声明；每类必须恰好有一条可重算的 rollout proof、controller admission 与 supervision dataset，缺失或重复均失败关闭。
- `machine_input_load` 与 `machine_capacity_establishment` 已完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 全链证明。支持动作的执行 run ID 必须从终端 native outcome 中排除，即使二者合法共享同一个高层 route occurrence。
- 修正支持请求读取了非权威 `time.time_of_day` 的错误；现统一读取透明桥 schema 的 `time.time`。文件夹具同时补齐必需的 `player.inventory_capacity`，没有放宽 required-state 或期限门禁。
- Release 构建、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。当时矩阵明确为 `2/5`，尚缺 `crop_planting`、`machine_input_material_transfer`、`machine_input_purchase`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用既有 `inventory.transfer_item -> executor.transfer_material` 及其 relocation settlement，建立 `machine_input_material_transfer` 的同型 file-backed 终端谱系证明；不得新增第二套搬运、寻路、ledger 或终端编译实现。

## 历史记录 · 2026-09-28 机器容量支持过渡接入终端 rollout 证明链

- 机器放置现以真实文件制品完成 `support request -> commit receipt -> shared compilation -> native execution receipt -> nonterminal settlement -> fresh replan`，并把这份精确血缘作为 `SupportingTransitionInitialProof` 传入既有终端执行、reservation 结算、rollout checkpoint、proof receipt、rollout admission 与 supervision dataset 构建器。没有新增 planner、ledger、动作编译器或执行器。
- fresh Teacher 会从放置后的快照与结算后 ledger 重建完整候选分母；本 fixture 最终选择可立即完成的 Beer 商店路线。监督数据只包含该终端原生结果，前置机器放置仅作为可验证上下文，绝不伪装成机器产出或 terminal native outcome。
- acquisition Teacher 编译边界现在根据目标、路线、候选、state/ledger、portfolio、ranking、角色和完整血缘生成稳定的 plan/queue/item/primitive ID；同一证明链重建不再受随机 GUID 干扰。商店报价读取器同时修正合法 `null` 交易数量的解析，测试快照补齐同一日期绑定的地图可行走证据，均保持失败关闭。
- 完整 `self-test-current-stage-one-collection` 已通过，覆盖新机器容量链及既有 train/validation/test continuation、语料、pairwise、live-shadow 和篡改拒绝。正式产品训练仍为 `false`。
- 下一固定切片是建立支持动作家族的终端训练谱系覆盖矩阵，并逐项证明 `crop_planting`、`machine_input_load`、`machine_input_material_transfer`、`machine_input_purchase`、`machine_capacity_establishment` 都能经同一 file-backed recurrence 接入终端 rollout；已有闭环只登记复用，不得复制实现。

## 历史记录 · 2026-09-27 机器容量制作后放置 hermetic 闭环

- 缺机器容量的 hermetic 路线现连续执行两次非终端支持过渡：先经既有 `executor.craft_machine_item` 得到机器，再用 fresh 快照和结算后 ledger 重新枚举既有 `executor.move_to_tile -> executor.place_machine` 队列。第二次原子提交把同一个 source-bound intent 从 `craft_selected` 推进到 `placement_bound`，没有创建第二个 intent 或第二套放置器。
- 两项放置回执保留真实中间移动 state hash/tick 边界；终态必须精确观测到背包机器减一、绑定目标格从无机器变成唯一同型空闲机器。相同库存变化但机器落在错误格子的负例会以 `supporting_transition_machine_placement_delta_mismatch` 失败关闭。
- 放置结算仍只写 machine-intent support marker，不伪造材料/货币消费或 route-complete；结算后的 state/ledger 能生成 fresh Teacher request，证明该生命周期已重新接入现有 portfolio replan 边界。Release 构建与完整 hermetic bootstrap 通过，正式训练仍为 `false`。
- 下一固定切片是把这份机器放置 settlement/replan 作为 file-backed `SupportingTransitionInitialProof` 的前置血缘，跑通随后的 terminal route、rollout checkpoint、proof receipt 与 supervision dataset；支持动作本身仍不得伪装成 terminal native outcome。

## 历史记录 · 2026-09-27 机器缺失容量支持递归闭环

- 目标日期机器路线在透明设施轴确认目标机器没有已摆放容量时，现在会发出精确的 `machine_capacity_establishment` 支持过渡。它只接受权威路线绑定的机器 qualified ID、规范化来源集合和确定性地点缺失证据；身份未知、来源多义或并非真实缺容量时仍在上游失败关闭。
- 候选、日计划和动作编译继续复用既有 `farm.establish_supported_machine_capacity` 生命周期：背包已有机器时进入原放置链，否则进入原制作链；放置之后的投料继续由既有机器支持链负责。没有新增制作、放置、投料或寻路执行器。
- 原子提交现可在同一 reservation portfolio 事务中建立唯一的 source-bound `MachineSupportIntent`。该分支允许没有材料/货币 claim，但只限精确机器容量请求；提交回执会重放并核对 intent。fresh 回执分别证明制作后的精确库存增量，或放置后的库存减一与目标格唯一空闲机器。
- 非终端结算使用 machine-intent marker，不伪造 reservation 消费；它只把同一个 active intent 重绑到 fresh state/ledger，并强制完整重规划。聚焦候选、Backend intent settlement、Core game-free `103/103`、Backend `219/219`、Release 构建和完整 hermetic bootstrap 均通过；正式训练仍为 `false`。
- 下一固定切片只补直接放置分支的完整 hermetic 端到端证据，并把该支持过渡纳入后续 portfolio rollout/proof 数据链；不得重新实现机器生命周期，也不得在证据闭合前授权正式训练。

## 历史记录 · 2026-09-27 机器触发器条件语义修复

- `Data/Machines.OutputRules[].Triggers[]` 的条件现在保留为显式 OR 备选集合，不再从目标日期链中丢失。单触发器的 `LOCATION_SEASON Target` 条件进入地点轴，并使用透明桥逐地点输出的 `effective_season` 精确判断；山谷冬季蜂房不会再被错误视为可产蜜，实际季节非冬的地点仍可通过。
- 多触发器只要包含跨轴地点或随机条件就失败关闭，不会把 OR 错压成 AND；未知 GSQ 谓词同样失败关闭。单触发器 `RANDOM` 概率由既有随机重试轴与输出项概率相乘；`ITEM_CONTEXT_TAG Input` 与 `ITEM_EDIBILITY Input` 继续由既有资源轴处理，没有复制第二套物品条件求值器。
- 修复只收紧静态日历与目标地点准入，不改变执行器、Teacher/Student 权限或正式训练门槛；`formal_training_authorized=false` 保持不变。

## 历史记录 · 2026-09-27 机器缺料采购递归闭环

- 机器资源轴缺料时不再直接终止：货币轴只对一个确定性的机器输入缺口查询当前原生商店预览，按总价、单价、商店和库存键稳定选择精确的正价金币报价。当前已有材料与未来采购严格分离；reservation 只占用已经实际存在的材料和完成全部缺口所需的金币，不虚构尚未购买的物品。
- processing 轴在存在采购前置时明确停在 `upstream_machine_input_purchase`。支持链复用现有 `economy.buy_supplies`，逐次执行跨图连接、柜台交互和单件购买；购买阶段仍使用既有 `DailyPlanCompiler -> ActionQueueCompiler -> executor.buy_shop_item`，并保留安全关菜单动作，没有建立第二套商店或购买执行器。
- route/交互进度要求金币和目标物品都不变，只把仍活动的 claims 重绑到 fresh state。购买回执要求精确金币减少一个绑定单价、玩家背包中精确 qualified item 增加一个绑定堆叠；共享 ledger 支持货币 claim 的部分消费与最终完成，完全耗尽的 claim 不得继续重绑。每个阶段结束后旧队列失效，并从 fresh snapshot 与新 ledger revision 完整重规划，直到材料足够后自然回到既有箱存暂存或机器投料链。
- Release 构建、bootstrap hermetic 与共享 ledger 聚焦回归通过；该链仍是非终端监督证据，`formal_training_authorized=false`。下一固定切片是把“缺少已摆放机器”的路线接入既有 `farm.establish_supported_machine_capacity` 生命周期并执行同样的提交、回执、结算和完整重规划，不新增制作、放置或投料实现。

## 历史记录 · 2026-09-27 机器箱存原料暂存与 reservation 搬迁闭环

- `resource_inputs` 仍优先使用当前角色背包，但现在允许当前地图内、执行者获权、普通、未锁且交互坐标完整的放置箱子证明机器路线材料充足；此类槽位显式标记 `requires_player_staging=true`，绝不直接编译为机器投料。
- 当背包没有可直接投料的已预留材料时，支持递归只生成一个确定性 `inventory.transfer_item` 候选，并复用现有 `executor.move_to_tile -> executor.transfer_material` 两步宏。原生投影必须把完整 claim 放入恰好一个玩家背包槽；跨槽拆分、非当前地图、特殊箱、锁定箱、歧义节点或容量不足均在上游失败关闭。
- fresh 前后快照回执逐节点和逐槽核验箱子减少量与背包增加量。共享 reservation ledger 新增原子 relocation：活动 claim 从原箱子槽搬到实际观测到的背包槽，数量不变、状态仍为 `Active`；它不是消耗，不写 route-complete。部分搬迁、消费与搬迁混用、目标槽未观测到足量物品均拒绝，成功后必须使用新 state hash 与 ledger revision 做完整重规划，随后才可进入既有机器投料支持动作。
- acquisition dispatch、bootstrap hermetic、Core game-free `91/91` 与 Backend `213/213` 已通过；正式训练授权仍为 `false`。下一固定切片是让已有权威商店报价和开放窗口的确定性材料采购复用同一非终端递归协议；已有机器容量生命周期只做接线复用，不建立第二套制作、放置或投料执行器。

## 历史记录 · 2026-09-27 机器投料部分结算与完整重规划复入

- 共享 `ReservationPortfolioLedgerService` 的非终端支持结算现已接受结构化材料消费数组。每项都必须绑定一个活动 reservation、精确库存节点/槽位/qualified ID，且消费量不得超过 claim 余量；重复 reservation、重复槽位、新旧表示混用、超量或溢出输入均原子失败关闭。旧单条全量消费请求继续兼容。
- 全量消费仍把 claim 标记为 `Completed`；部分消费只扣减 `Quantity`、保持 `Active`、递增 claim revision，并分别记录 `material_reservation_consumed` 或 `material_reservation_partially_consumed`。一次动作包含主料和附加料时，所有消费在同一个 ledger revision 结算，只写一个非终端 support marker，不写 route-complete marker。
- 实验控制面不再假设支持动作必为种植。作物与机器都从各自已验证的 fresh 前后态回执生成同一种 canonical 消费集合；结算回执逐项核对余量、状态、history 与精确重放。机器两次投料场景的第一次动作现保留一份活动余量，并使用结算后 ledger revision 和 fresh 机器快照复入既有完整 Teacher 重规划链。
- 聚焦 acquisition dispatch、bootstrap hermetic、Core game-free `91/91` 与 Backend `209/209` 已通过。该闭环仍只提供非终端监督证据，`formal_training_authorized=false`；下一固定切片是把同一支持递归协议扩展到需要先采购输入或建立机器容量的上游支持动作，而不是绕过候选、reservation 或原生执行回执。

## 历史记录 · 2026-09-27 机器投料支持回执：真实扣料与机器加工态

- 现有 `acquisition_route_supporting_transition_receipt.v1` 已从作物专用核验扩展为按 `crop_planting / machine_input_load` 显式分派，仍只接受一个经过哈希绑定的非终端队列项；畸形的空队列或多项队列只会失败关闭，不会抛异常或被误判为完成。
- 机器请求和队列现在携带逐 claim、逐库存节点、逐槽位的单次材料消费计划。回执要求每个槽位的前后数量差精确等于计划消耗量，并要求目标机器从空闲态进入加工或已完成态；机器类型、最后输入、持有输出、唯一权威 route kind/source ID 及原生剩余计时必须全部与编译血缘一致。
- 透明桥新增原生 `last_input_item` 与 `last_output_rule_id` 投影，回执不靠显示名或预计收益猜测投料结果。资源轴同时修正为只把当前执行者的玩家背包视为原生投料来源；箱子里的材料必须先经过取物动作进入背包，不能直接满足机器输入。
- 聚焦 acquisition dispatch、GoalConditionedBootstrap Release 与 TransparentBridge 真实游戏引用构建均通过，均为零警告、零错误。正式训练授权仍为 `false`；下一固定切片是让共享 reservation 结算支持一次动作只扣除已核验的部分数量、保留 claim 余量，并用结算后的 ledger 和 fresh 快照复入既有完整重规划链。

## 历史记录 · 2026-09-27 机器投料支持请求：期限、材料占用与提交门控

- 现有作物支持协议没有被复制。`acquisition_route_supporting_transition_request.v1` 新增 `crop_planting / machine_input_load` 类型分派；机器分支只接受已经由唯一原生 route kind、source ID 和输出 qualified ID 命中的 `load_machine_input_tile` 候选。
- 投料候选现在结构化携带机器 qualified ID、原生触发器 `RequiredCount` 和探针执行 `ReadyTimeModifiers` 后的有效加工分钟。请求会把地点、格子、机器类型、输入槽、输入物品、单次消耗量和处理轴的 `machine_schedule_binding` 逐项重绑；任何数量、时长或槽位漂移都在原子提交前失败关闭。
- 完成日计算与当时 1.6.15 反编译语义一致：`DaysUntilReady` 按 `Utility.CalculateMinutesUntilMorning` 转换，普通分钟计时跨 2:00/6:00 边界继续递减。只有有效时长与处理轴权威值完全一致、预计完成日落在显式期限和权威窗口内时才放行；未绑定时间修正器、动态输出方法和仅隔夜完成仍由处理轴阻塞。
- 路线的完整材料 claim set 继续通过共享 `ReservationPortfolioLedgerService` 一次性预检和提交；当前动作只绑定其中与候选槽位相同的单次主输入量。提交回执之后仍复用同一 `DailyPlanCompiler`、`ActionQueueCompiler` 和 `executor.load_machine_input`，不新增机器专用执行器，且保持非终端、成功后强制新快照与完整重规划。
- 聚焦 dispatch、bootstrap hermetic、Core game-free `91/91` 与完整 `experiments/Run-Regression.ps1` 均通过。下一固定切片是核验投料前后库存与机器状态，并让结算只扣除本次观测到的输入数量、保留同一 claim 的剩余数量；在回执、部分结算和复入链闭合前，正式训练授权仍为 `false`。

## 历史记录 · 2026-09-27 机器投料支持过渡：精确来源与共享编译链

- 透明桥现在把投料探针已经选中的 `MachineOutputRule` 与 `MachineItemOutput` 重新绑定到 `Data/Machines` 的原生规则索引，并在预测结果中输出唯一的 `authoritative_route_sources`。有稳定规则 ID 时沿用 `machine_output` 身份；无规则 ID 时按精确 rule/output 索引输出 `native_machine_item_query_output` 或 `native_machine_flavored_output`。规则、输出行或目标物品不能唯一绑定时保持空来源并失败关闭。
- Core 的 `load_machine_input_tile` 候选保留上述结构化来源及预测输出物品；目标日期支持选择同时核对输出物品、route kind 与 source ID，不按显示名、物品相同或预计收益猜测。错误输出、错误规则与同一目标物品的多来源歧义均不能进入队列。
- 三类机器路线的 lowering 复用现有 `farm.process_machines` 候选作为原子投料支持动作，再经同一 `DailyPlanCompiler` 与 `ActionQueueCompiler` 编译为 `executor.load_machine_input`。该动作角色为 `supporting_transition`，不允许终端回执，并要求成功后读取新快照、完整重规划；`farm.establish_supported_machine_capacity` 与经济型 `farm.load_supported_machine_input` 仍保留，机器制作/放置没有证据时继续失败关闭。
- 聚焦 dispatch 与 hermetic 自测、Core/TransparentBridge Release 构建均通过。该切片闭合“已有空闲机器的精确投料候选到原生动作队列”；机器专属完成期限、原子材料 claim 与提交门控已由上方后续切片接入共享协议，投料后机器状态回执及部分 claim 结算仍待闭合；正式训练授权仍为 false。

## 历史记录 · 2026-09-27 机器既有产物抵扣：下游闭环

- processing 轴会在同一份透明机器快照上重新核验物品、堆叠、品质以及唯一 route kind/source ID，并按地点与格子稳定分配 `credited_existing_output_quantity`。部分抵扣只排程剩余尝试；完全抵扣保留显式零尝试 binding，并以既有机器计时器作为最迟完成时点。
- stochastic 轴只让未抵扣余量承担原生输出选择概率。完全抵扣的手动机器为零基线、零重试；部分抵扣扩张时使用“已抵扣数量 + 重试次数 * 权威最小产量”重新经过资源、货币、原子预留和 processing，抵扣量漂移即失败关闭。
- daily 轴把抵扣转换为真实动作队列：旧产物先收取并按精确抵扣量记账，剩余尝试继续使用既有投料/等待/收取状态机；完全抵扣只生成收取动作。无关旧产物仍只负责腾出机器，不会误算目标产量；手动路线必须同时满足所有逐机安排完成与目标数量收齐。
- Release 构建、hermetic、Core game-free `91/91` 与完整 `experiments/Run-Regression.ps1` 均通过（退出码 `0`），覆盖无抵扣、部分抵扣、完全抵扣和随机重试扩张场景；训练授权仍保持关闭。

## 历史记录 · 2026-09-27 机器既有产物上游抵扣：资源余量

- `resource_inputs` 只汇总 facility 轴中 `machine_active_output_route_matches=true`、物品一致且品质达标的旧产物，并将抵扣量封顶为当前需求；剩余产量再按权威最小堆叠换算投料次数。
- 部分抵扣会同步缩减主投入与全部附加消耗，且每个 machine binding 保留 `credited_existing_output_quantity`；完全抵扣生成显式零尝试/零消耗 binding，不要求虚构一个可用材料槽。来源不匹配或品质不足仍按完整需求投料。
- Release 构建与 hermetic 的无抵扣、部分抵扣、完全抵扣回归通过；processing、随机重试和 daily 日程现已共同消费该 credit，细节由上方“下游闭环”记录。

## 历史记录 · 2026-09-27 机器既有产物上游抵扣：设施证据

- `facility_capacity` 现在随逐机目标保留透明 `held_item` 的精确物品、堆叠和品质；只有 `active_output_authoritative_route_sources` 恰有一个来源，且 route kind、source ID、输出物品都与当前权威路线一致时，才标记 `machine_active_output_route_matches=true`。
- 该切片只建立抵扣证据，不在设施轴提前改变材料需求。同一机器仍可作为生产设施使用，但缺失、多义或不匹配的旧产物不会获得抵扣资格；后续固定切片由 `resource_inputs` 计算剩余需求并让下游轴共同消费同一结果。
- GoalConditionedBootstrap Release 构建为零警告，hermetic 覆盖精确命中与错误 source ID 不抵扣。完整回归在 resource/downstream 闭环后统一执行。

## 历史记录 · 2026-09-27 机器日内移动与交互预算

- 机器 processing binding 新增逐次 `attempt_schedule`，保留全局尝试序号、单机尝试序号、处理开始偏移和完成偏移；随机重试扩量后的每次投料不再只剩一个汇总计数。日内轴直接消费该排程，不重新猜测尝试如何分配到机器。
- 手动机器路线会从同一快照位置出发，使用既有路线图、日期证据和移动校准，逐次生成 `load_machine_input -> 原生加工等待 -> collect_machine_output`；多台机器可并行加工，但每次投料和收取都必须重新证明可达站位。自动触发机器则等待透明桥已绑定的目标产物完成时点，再移动并收取足量产物。
- `MachineInteractionBudgetPolicy` 现在是投料/收取 30 tick 估时的唯一常量来源，动作编译器与 Teacher 日内预算共用它。玩家动作时间只统计投料和收取；机器自身加工等待只推进保证完成时刻，不会重复计入 `terminal_action_game_minutes`，机器交互原生不消耗体力。
- 本切片先让快照时 `processing` 或 `ready_output` 的手动机器失败关闭，空闲手动机器和已唯一绑定输出的自动机器聚焦测试通过，Core 回归为 `2570/2570`，完整 `experiments/Run-Regression.ps1` 回归通过（退出码 `0`）；紧随其后的“非空闲手动机器复用”切片在同一日程状态机内闭合该分支。

## 历史记录 · 2026-09-27 非空闲手动机器复用

- processing/daily 两轴继续共用同一份机器快照与逐机 attempt schedule；daily 轴现在能按 `minutes_until_ready` 等待已有产物，执行现有 `collect_machine_output`，再执行现有 `load_machine_input`，最后等待并领取本轮目标产物，没有引入第二套机器动作。
- 复用前必须存在精确 `held_item` 身份、数量和品质，并按每台待清空机器保守预留一个透明 `player.inventory_capacity.empty_slots`；容量不足或字段不一致时上游直接失败关闭。只有设施、资源、processing、随机重试和 daily 各轴都核验为同一路线的既有产物才计入目标；其他旧产物仍只清空机器，不抵扣输入。
- 本切片聚焦回归覆盖 `processing` 与 `ready_output` 两种初态的“等待/领取/投料/领取”顺序和零空格拒绝；Release 构建为零警告，`self-test-bootstrap-hermetic` 与完整 `experiments/Run-Regression.ps1` 均通过（退出码 `0`）。

## 历史记录 · 2026-09-27 机器随机输出概率预算

- `Data/Machines` 的每条静态机器来源现在保留同一原生规则的完整输出选择行，而不再只有目标行和行数。锁定版反编译证明：`UseFirstValidOutput=true` 按顺序返回第一个通过 `GameStateQuery` 的输出；否则从全部有效输出中均匀选择；`RANDOM p` 通过该次上下文的 `Random.NextDouble()` 判定。
- 随机轴只解析可严格证明的形态：单行、只含简单 `RANDOM p` 的首个有效链，以及全部条件恒真的随机有效集合。首个有效行的概率为前置各行失败概率乘目标行通过概率；随机有效集合为 `1/N`。复合条件、条件化随机有效集合、随机物品查询、输出回调或逐物品条件继续 fail-closed。
- `processing_lead_time` 的“目标日可领取”与“快照时已落地”已拆成独立事实，避免把当天稍后完成的机器产物误判为现有库存并跳过随机预算。已由透明桥唯一绑定的自动机器在制产物另按“输出选择已完成”处理，不重复抽概率。
- 随机预算新增基础尝试次数，额外重试改为 `总尝试次数 - ceil(需求数量 / 保证最小堆叠)`，不再错误地直接减需求数量。概率预算扩大机器投料或处理次数时，扩大后的总尝试数会重新进入资源、货币、原子预留和机器排程轴，不能沿用基础 claim 放行。
- Release 构建、hermetic 机器概率测试和完整 `Run-Regression.ps1` 均通过；全量回归覆盖 1599 条路线、train/validation/test continuation 与伪造/篡改拒绝。下一固定切片是把已排程的机器投料与收取交互接入日内移动、动作时间和体力预算，且不得把机器自身等待时间重复计为玩家动作时间。

## 历史记录 · 2026-09-27 机器随机重试增量预留闭合

- 额外随机尝试不再停留在“需要重验”的标志位。随机轴按 `所需总尝试数 * 原生保证最小产量` 构造扩大后的路线需求，并复用现有 `resource_inputs -> currency_budget -> inventory_reservation -> processing_lead_time` 唯一链路；没有新增第二套材料选择、claim 或机器调度算法。
- 扩大后的资源轴按总尝试数重算主投入和全部附加消耗，reservation 只能从资源轴已证明的精确节点/槽位生成原子 claim；材料不足、现有 claim 冲突、货币不足、机器容量不足或排程次数漂移都会保留对应轴的原始原因并失败关闭。
- 随机轴成功时直接把扩大后的 processing route 作为自己的 `upstream_route`，因此后续日内预算、机会成本、组合提交和执行绑定会自然遍历到扩大后的 claim set，不通过旁路字段携带第二份预留。聚焦测试锁定 claim 数量和机器排程次数均等于概率预算的总尝试数。
- GoalConditionedBootstrap Release 构建、hermetic 自测和完整 `Run-Regression.ps1` 均通过；完整回归覆盖 train/validation/test continuation、组合语料以及伪造/篡改拒绝。正式训练准入仍为 false；下一固定切片是机器投料/收取的日内动作预算。

## 历史记录 · 2026-09-27 机器处理提前量闭合

- 三类 `Data/Machines` 路线已从资源预留继续接入既有 `processing_lead_time` 轴，没有新增第二套调度器。手动投料路线复用资源轴证明的加工次数，并按设施轴锁定的真实机器位置、当前 `idle / processing / ready_output` 状态与剩余分钟并行排程；机器正在加工时，现有剩余占用时间会先从当日容量中扣除。
- 计时语义按 1.6.15 反编译锁定：`DaysUntilReady >= 0` 优先于 `MinutesUntilReady`，分钟计时只在当前可游玩日剩余时间内判定同日完成，交互耗时仍归下游 `daily_time_energy_budget`。当前 188 条权威机器路线全部为 `OnlyCompleteOvernight=false` 且没有 `ReadyTimeModifiers`；未来模组或版本若出现过夜限定、时间修正器或 `OutputMethod` 时间覆盖，在对应语义接入前会明确阻塞。
- 透明桥新增 `active_output_authoritative_route_sources`，把加工中或待收取产物绑定到原生 route kind、source ID 与物品 ID。自动触发机器只有在该绑定唯一且与当前路线完全一致时，才允许把在制产物算作目标日产出；同物品多来源、旧快照缺字段或来源无法唯一归属均 fail-closed。
- hermetic 测试已覆盖两台机器并行同日完成、单机容量不足、`DaysUntilReady` 跨日、自动触发在制产物精确命中及来源不明阻塞。GoalConditionedBootstrap 与 TransparentBridge Release 构建均为 `0 warning / 0 error`。下一固定切片是把机器随机输出的额外尝试数反向增量绑定到材料、机器处理时间和日内动作预算，随后才允许这类路线进入完整 Teacher 候选比较。

## 历史记录 · 2026-09-27 机器投入资源与非消耗库存条件

- 机器三类来源现已从 `facility_capacity` 接入唯一的 `resource_inputs` 轴。只有带 `ItemPlacedInMachine` 位的原生触发器会产生投料需求；纯 `DayUpdate`、`OutputCollected` 或 `MachinePutDown` 触发器不会错误扣除 `AdditionalConsumedItems`。同一路由混合自动与投料触发器时继续失败关闭，等待静态路由按触发方式展开。
- 资源轴按权威最小产出量计算满足目标数量所需的基础加工次数，再将主投入的 `RequiredCount` 和煤等全部 `AdditionalConsumedItems` 按次数放大。随机成功所需的额外尝试仍由后续 `stochastic_retry` 轴独立追加，当前结果不提前冒充完整随机预算。
- `farm.material_inventory_graph` 的每个槽位新增原生 `Item.GetContextTags()`、对象 `Edibility` 及各自投影状态。标签型和可食用度型机器规则必须找到同一槽位集合中的真实输入见证；旧快照或不完整元数据只能支持无需这些字段的精确 ID 规则，不能把空标签猜成“不匹配”。
- 机器资源结果保留所选原生触发器、每次数量、加工次数和允许预约的精确节点/槽位。后续 reservation 只能从这组槽位扣量，避免资源求值与实际预约使用不同物品。`PLAYER_HAS_ITEM` 资源条件也已按反编译语义接入，但它只读取当前玩家随身库存节点、保留否定与最小/最大数量，且不创建消耗预约；原生核桃/Qi 宝石特例在相应货币证据接入前失败关闭。
- Release 构建、机器资源 hermetic 自测和 Core `2570/2570` 回归通过。下一固定切片是机器 `processing_lead_time`：使用同一 `machine_source` 的分钟/天数、过夜完成和时间修正器计算最迟投料时点，再进入机器随机输出的重试与资源增量闭环。

## 历史记录 · 2026-09-27 机器来源地点与设施容量绑定

- `Data/Machines` 静态来源现已接入既有 `location_route -> facility_capacity` 轴，不新增第二套路由或执行器。地点轴只接受同一透明快照中 `farm.machines[]` 的完整、未截断舰队，按机器 qualified ID、地点和格子绑定来源；空舰队是有证据的来源缺失，字段缺失、行数漂移或重复地点格子则失败关闭。
- 设施轴会在同一快照中再次按地点和格子重绑机器，保留 `idle / processing / ready_output` 状态并要求该机器原生声明可产出。它只证明现有机器容量，不推测制作、摆放、投料、吞吐量、完成时间或随机成功率；这些仍分别归资源输入、处理提前量和随机重试轴。
- Release 构建和 hermetic 机器舰队/地点/设施测试通过。现有历史训练快照不能提供同一时刻的新版全地图日期路线与完整机器舰队，因此真实 188 条机器路线仍按缺失证据阻塞；回归脚本锁定这一结果，禁止把两份不同快照拼成伪证据。
- 机器资源输入已由上节闭合；当前下一固定切片是机器处理提前量，随后才进入随机输出预算。

## 历史记录 · 2026-09-21 单路线 reservation 结算与重放回执

- 新增 `POST /api/v1/strategy/commitments/reservation-portfolios/settle-completed-route`。请求必须绑定 fresh after snapshot、当前 ledger revision、portfolio/goal/route source decision、fresh terminal receipt 的小写 SHA-256，以及该路线当前全部 active 材料/货币 reservation ID。服务在内存副本中一次性把精确集合改为 `completed`，记录完成原因和证据哈希，只前进一个 revision，并写入逐 claim 历史与唯一 `reservation_portfolio_route_complete` 标记；缺少、多余或重复 ID 均整体拒绝。无 claim 路线仍写入可审计标记。
- 新增 `acquisition_route_portfolio_settlement_receipt.v1`、`build-acquisition-route-portfolio-settlement-request` 和 `build-acquisition-route-portfolio-settlement-receipt`。请求生成器先确定性重建 execution binding 与 fresh terminal receipt，再从已提交 ledger 推导完整 active claim set；回执随后用历史标记时间精确重放 Core 事务，要求 settlement result、settled ledger 和重放结果逐对象一致。额外历史行篡改会被拒绝。
- Backend 全量回归为 `202/202`；实验项目 Release 为 `0 warning / 0 error`；聚焦 76 路线链通过真实商店路线的 `revision 1 -> 2`、两条 claim 完整结算、exact replay 与篡改拒绝。该回执只证明一条已选路线完成后的 reservation 生命周期，强制 `fresh_replan_required=true`，不代表组合已完成，`formal_training_authorized` 仍为 false。

## 历史记录 · 2026-09-21 路线组合 Teacher 偏好与 caller 绕过关闭

- 新增 `acquisition_route_portfolio_teacher_preference_request.v1`、`acquisition_route_portfolio_teacher_preference.v1` 与 `build-acquisition-route-portfolio-teacher-preference`。请求只声明目标、同一快照/ledger revision 和需求组 scope；构建器从权威 selection rule 与当前全部目标日 Pareto route occurrence 自动枚举组合，不接收调用方候选列表，也不读取 learner rank/score。
- `all_required` 枚举全部替代项，`choose_at_least_required_slots` 枚举从最低所需槽位到全部可行替代项的所有组合；每个替代项再与全部 Pareto 路线做笛卡尔积。候选分母超过 `4096` 时整体失败而不截断。每个生成 proposal 都复用唯一 portfolio admission 与原子 preflight；不可用组合只作 defer，不生成负标签。
- Teacher 只在一个 admitted 组合的非标量聚合成本向量严格 Pareto 支配其他全部 admitted 组合时给出偏好。相等或耗时/体力/材料/原生货币互有取舍时显式阻塞，不按 proposal ID、learner 分数、售价汇总或跨货币换算破平局。选中 proposal/admission 可直接按规范 JSON 落盘并由原构建器逐对象重建。
- `acquisition_route_execution_binding.v1` 现在还会重算完整 Teacher preference，并要求实际 proposal/admission、commit receipt 与 preference 的唯一选中结果一致；合法但由 caller 私自选择的组合不能借用后续 reservation 回执进入执行。聚焦 76 路线全链与严格支配/相等/互有取舍纯回归通过，Release 为 `0 warning / 0 error`。正式训练仍为 false；下一固定边界是组合内顺序执行、逐路线 fresh 重规划、reservation 生命周期和组合级完成回执。

## 历史记录 · 2026-09-21 路线组合原子提交回执与执行所有权闭合

- `acquisition_route_portfolio_commit_receipt.v1` 会从原始 proposal、完整目标日依赖链、提交前 ledger、admission、提交结果和提交后 ledger 确定性重建组合提交事实。发生写入时必须只前进一个 revision，全部材料/货币 claim 必须成为精确 active 行，全部显式 release 必须取消，并且每个分量历史与唯一 `reservation_portfolio_commit` 标记必须处于同一 revision；任一后置 claim 失败时不得泄漏前置写入。无需新 claim 的组合只接受规范 JSON 等价的未变化 ledger 和无 commit result 的幂等路径。
- `acquisition_route_execution_binding.v1` 现已消费并重算上述回执。被调度的路线必须在该组合中精确出现一次；每个 normalized command 除原有 11 个路线身份参数外，还必须携带精确 `acquisition_reservation_portfolio_id` 与 `acquisition_reservation_ledger_revision`。因此旧队列、只做过 preflight 的组合、错误 revision 或脱离组合选择的单路线均不能绕过 reservation 所有权边界。
- Backend 回归为 `199/199`；实验项目 Release 构建为 `0 warning / 0 error`；聚焦 76 路线全链自测通过真实商店材料+货币原子提交、提交后回执、篡改 ledger 拒绝、无 reservation 幂等回执、缺组合所有权参数拒绝和 fresh 终态回执。未启动游戏或训练。
- 这一步完成的是“组合 claim 原子落账 -> 提交后证明 -> 单路线队列持有组合所有权”。独立 Teacher 的完整有界候选枚举、唯一严格 Pareto 偏好和执行绑定已由上节接入；相等/不可比组合继续阻塞。剩余固定边界是组合内多路线的顺序执行、fresh 重规划、reservation 生命周期和组合级完成回执；这些完成前 `formal_training_authorized` 继续保持 false。

## 历史记录 · 2026-09-21 目标日路线执行绑定与 fresh 终态回执闭合

- 新增执行前 `acquisition_route_execution_binding.v1` 和命令 `build-acquisition-route-execution-binding`。构建器会从原始输入确定性重建完整机会成本报告，只允许完整 Pareto 前沿中的一个精确 `route_occurrence_id` 进入执行；候选 ID 由 occurrence 确定生成，每个队列项必须重复携带精确需求、路线、来源、数量和品质身份，同时哈希绑定 lowering、目标日快照、动作队列、全部队列项和 option。每个 option 必须属于该路线权威 endpoint/support 集，且至少包含一个 endpoint，不能再用调用方别名或物品 ID 猜测实际执行了哪条获取路线。
- 新增执行后 `acquisition_route_fresh_terminal_receipt.v1` 和命令 `build-acquisition-route-fresh-terminal-receipt`。它会重算并逐对象核对执行绑定，复用统一的顺序队列回执验证器，要求同存档/玩家、同目标日、新鲜 after snapshot、精确状态哈希和 tick 边界、逐项身份/顺序/primitive 原生验证以及最终队列闭合。
- `item_id` 路线必须证明满足最低品质的玩家库存净增量不少于完整 `required_amount`；“要求 2 个但只增加 1 个”已成为显式失败回归。`money_payment` 路线另走社区中心原生 ingredient `false -> true` 与 money 精确减少量双重验证，不冒充库存产物回执。原 Stage 1 Teacher 回执也改用同一精确数量验证器，修复了过去任意正增量即可放行多数量需求的问题。
- Release 构建为 0 warning / 0 error；聚焦 Bootstrap 76 路线全链自测通过，覆盖确定性绑定、真实商店 Pareto 路线正向回执以及部分数量拒绝。机会成本的延后回归已完成，12 个固定依赖轴至此都有明确契约边界。
- fresh 回执只证明“这条已选路线的这一队列确实完成并产生了要求的终态”，不能倒过来替代路线选择。跨需求路线组合、原子 reservation commit、提交后所有权回执及其单路线 dispatch 绑定已由上节闭合；`route_training_evidence_eligible=true` 仍不等于正式训练准入，剩余边界是独立 Teacher 组合偏好、组合内顺序执行/fresh 重规划、组合级完成回执和正式 rollout controller 准入。

## 历史记录 · 2026-09-20 目标日机会成本轴实现待回归

- `feat/target-date-opportunity-cost` 已接入 `acquisition_route_target_date_opportunity_cost.v1` 与 `build-acquisition-route-target-date-opportunity-cost`。它会确定性重建完整 `daily_time_energy_budget` 上游、核对同一快照身份，并保持全部 route occurrence；上游报告漂移或被篡改时直接拒绝。
- 机会成本不压成一个人为分数。向量独立保留保证耗时、原生体力、按 `qualified_item_id + quality + live sale_price` 分开的精确物料数量，以及 money、star tokens、club coins、Qi gems 四个原生货币域。物料售价合计只作审计摘要，不代替物品身份，也不进行跨货币换算。
- 比较严格限制在同一 `(requirement_set_id, requirement_id, alternative_index)` 内，只使用严格 Pareto 支配：所有维度不更差且至少一维更好才淘汰；等价向量和互有得失的路线都保留在前沿。Learner 分数、未来收入和推测效用不得进入本轴。
- 库存成本从 claim 指向的实时材料槽读取；`quality` 或 `sale_price` 缺失、槽位/物品/状态哈希漂移、未知货币域、溢出或非正 claim 均失败关闭。fixture 已补显式成本字段，并新增两条当前可行路线及三类 Pareto 边界自检。
- 该轴的延后回归已于 2026-09-21 随 fresh 终态回执聚焦全链自测完成；机会成本报告仍只提供候选前沿，不直接选择或执行路线。

## 历史记录 · 2026-09-13 目标日日级时间/体力预算轴闭合

- 新增 `acquisition_route_target_date_daily_time_energy_budget.v1` 与 `build-acquisition-route-target-date-daily-time-energy-budget`。构建器会确定性重建完整随机重试预算，核对静态日历、钓鱼概率、快照、路线计时校准及全部 76 条 route occurrence 的身份；上游报告漂移或被篡改会直接拒绝，不能成为训练输入。
- 当前上游匹配的两种终端均有保守预算。普通无饵钓鱼必须从同一快照到达概率投影指定的精确合法站位，14 次尝试完整计入最大首次咬钩等待、原生抛竿/上钩/完美锁定/收杆时长，并按反编译 `max(0, 8 - effective_fishing_level * 0.1)` 计算 105 体力及至少 1 点保留体力；未证明 Efficient 时不推测其零消耗。商店购买必须到达权威交互格的相邻站位，并按现有 rolling 编译链的菜单等待、交互、对话、逐次购买和关闭菜单最坏时长计入完整数量。
- 路线抵达与终端动作必须整体落在同一个已由上游目标日解析的来源窗口内。钓鱼临时等级加成、带饵/定向鱼饵、固定种子跨捕获状态及尚未实现的其他活动终端继续显式阻塞；本轴不选择替代路线、不组合日计划、不计算机会成本，也不证明 fresh 产物回执。
- 聚焦 fixture 现为 `76/76` 已解析、2 条匹配、74 条上游不适用、0 条阻塞；钓鱼和商店预算分别验证精确站位、抵达/完成时刻、尝试数量、体力与编译时长，篡改随机预算报告会被确定性重建拒绝。Core game-free `77/77`、Backend `194/194` 及 Bootstrap 全链自测通过，实验项目 Release 构建为 0 warning / 0 error；训练授权保持 false，未启动游戏或训练。
- 下一固定轴是 `opportunity_cost`，随后才是 fresh 终态回执。当时成果只证明“若选择这一条路线，单日时间和体力是否可行”，不能提前宣称 Teacher 路线组合或正式训练准入完成。

## 历史记录 · 2026-09-14 普通无饵钓鱼重试预算闭合

- `fishing_forecast` 已成为按 `location_id + rod_slot_index` 隔离缓存的轻量透明快照。它只运行 world/fishing 必需读取器，不再连带扫描玩家全库存、菜单、农场、NPC 或全部地点；目标地点必须已经加载，错误地点、鱼竿槽位、跨存档/玩家/日期/时刻或超过 30 tick 的预测都会失败关闭。
- 透明桥按锁定版反编译公式精确发布 `PreciseFishCaught * 859` 固定种子规则的本次通过/失败，并区分原生连续随机。Core 对指定落点、站位、完整 Default+地点规则、优先级、同级最坏顺序、随机物品选择和 `Data/Fish` 接受概率生成首轮保守下界；固定种子不能冒充可重复概率。
- 重复抛竿另有独立证明：当前只放行明确无饵、无魔法鱼饵/好奇诱饵、无临时钓鱼等级加成，且目标及所有前置竞争规则均无可变条件、逐物品条件、捕获上限、捕获标志、未来等级解锁或固定种子的上下文。单次概率可读但重复性不成立时，随机预算轴仍阻塞。
- `StochasticRetryPolicy` 以统一 95% 阈值计算多成功事件的最小二项重试次数，使用对数域下尾求和与有界二分；额外重试数已从错误的 `总尝试-1` 修正为 `总尝试-所需产物数`。聚焦 `(O)145` 路线以单次保守下界 0.2 得到 14 次总尝试、13 次额外尝试；因明确无饵，不扩大材料预留。
- 76 条目标路线现为随机预算轴 `76/76` 已解析、2 条目标日匹配、0 概率阻塞，训练授权仍为 false。Core game-free `58/58`、Backend `194/194`、Bootstrap 全链自测、实验项目和真实程序集桥接构建均通过；处理报告或钓鱼概率报告被篡改都会被确定性重建拒绝。
- 下一固定轴是 `daily_time_energy_budget`：必须把选中终端站位接入可达性、把 14 次尝试换算为同一鱼类开放窗口内的保守时间/体力预算，再进入机会成本和 fresh 终态回执。带饵重试、定向鱼饵第二轮以及固定种子跨捕获状态仍保持显式阻塞，不能在本轴偷放行。

## 历史记录 · 2026-09-13 Master Angler 原生地点鱼概率输入目录

- 权威需求清单新增独立的 `native_fish_spawn_chance_rule` 证据，哈希锁定反编译 `SpawnFishData.cs`，并逐段守卫 `GetChance` 的基础概率、好奇诱饵、每日运气、数量修正器、特定鱼饵和运气等级公式。原有 `native_location_spawn_rules` 证据同时明确覆盖 `GameLocation.getFish` 的规则排序与解析入口。
- `master_angler_opportunity_catalog.v1` 现在对全部 180 条匹配原生地点规则保留直接/随机物品选择、`Chance`、每日运气开关、好奇诱饵修正、特定鱼饵加成与倍率、运气等级加成、完整数量修正器、修正器模式和 `UseFishCaughtSeededRandom`。真实 1.6.15 目录为 174 条直接选择、6 条随机选择、14 条精确鱼获计数种子随机，静态输入未解析项为 0。
- 新完整性门会拒绝缺少这些字段却仍声称 `complete` 的旧目录；Master Angler 窗口、获取日历和当前 Teacher 前沿均继承此门。合成回归另外覆盖当前 180 条原生规则未使用的 `ChanceModifiers` 分支，避免未执行代码冒充支持。
- 本步只闭合“目标鱼匹配规则的静态 spawn-chance 输入”，不等于目标投点的终端捕获概率。当时后续计划必须按目标地点和投点组合 Default 与地点全部竞争规则，执行原生 `Precedence + 随机同级顺序`、两轮定向鱼饵回退、ItemQuery 与 `CheckGenericFishRequirements`，再生成单次目标产物概率；在此之前随机重试轴继续失败关闭，训练授权保持 false。

## 历史记录 · 2026-09-13 显式目标日期随机重试预算轴首段

- 新增 `acquisition_route_target_date_stochastic_retry_budget.v1` 与 `build-acquisition-route-target-date-stochastic-retry-budget`。构建器会逐对象重建处理提前量报告并核对静态来源，`uncertainty_mode` 也从 lowering 开始无损穿过静态日历、目标日日历和解锁记录，避免末端按 route kind 临时猜测随机性。全部 33 种 route kind 均有唯一随机性分类，分类漂移会直接拒绝输入。
- 确定性 fresh receipt 与处理轴已证明的保守产出使用零重试预算；原生随机路线若目标产物已在透明快照中物化，也可按实际数量和品质零重试领取。其余原生随机路线必须给出目标地点、目标终端动作的精确单次概率、产量、品质和条件上下文；当前地点钓鱼投影不能冒充远程地点概率。
- 95% 成功阈值已抽到 Core 唯一的 `StochasticRetryPolicy`，现有蟹笼容量评估与新目标日期轴共用，不保留第二个常量。重试若增加鱼饵、晶球或其他耗材，在重新通过资源、货币和原子预留校验前仍不能授权执行。
- 聚焦 fixture 保留 `76/76`：74 条处理轴不适用，商店确定购买路线零重试通过，唯一可达的普通钓鱼路线因缺少 Beach 目标投点概率显式阻塞；篡改处理报告会被确定性重建拒绝。训练授权仍为 false，下一修正是补齐可跨地点查询的钓鱼终端概率证据，再计算多成功次数所需的重试预算与耗材回环；之后才进入日时间/体力轴。

## 历史记录 · 2026-09-13 目标路线数量与最低品质契约修复

- 权威 lowering 中的 `match_kind`、`amount` 和 `minimum_quality` 现在从静态来源解析开始无损进入日历、解锁及全部后续目标日期制品；每个阶段都会把嵌套路线与原始静态行逐字段核对。已删除货币轴末端临时回查数量的 `AcquisitionRouteAmountIndex`，不再允许中间轴把“需要多份/指定品质”退化成“一份普通品质”。
- 作物静态证据直接读取 `Data/Crops` 的 `HarvestMinStack`、`HarvestMaxStack`、`ExtraHarvestChance`、`HarvestMinQuality` 和 `HarvestMaxQuality`。设施容量按 `ceil(required_amount / HarvestMinStack)` 汇总所有匹配地点的现存目标作物槽与开放备耕槽；种子需求只扣除最低产量可保证的现存作物产出。现有逐格状态还不能证明高于静态最低值的收获品质，因此高品质作物要求会显式阻塞，不能用概率或示例猜测通过。
- 商店报价沿用透明桥现有的原生 `stack` 与 `quality`，购买次数为 `ceil(required_amount / output_stack)`，价格、有限库存和交换物数量按次数同步放大，并拒绝低于最低品质的商品。处理轴汇总成熟作物及蟹笼的可证明堆叠和品质；透明桥为蟹笼补充 `current_output_stack/current_output_quality`，不足数量不再因任意一个目标产物已就绪而通过。
- 聚焦夹具将同一需求固定为 2 份且最低品质为非零，验证 2 个作物槽、2 枚种子、10 木材交换物、200g、2 次有限库存购买、2 份成熟产出及蟹笼数量/品质过滤；GoalConditionedBootstrap Release 和聚焦自测通过，训练授权仍为 false。本次没有启动游戏或训练。
- 下一固定轴仍是 `stochastic_retry_budget`，但不得直接附加在当前预留结果后授权执行。随机尝试数若增加种子、鱼饵、晶球或其他耗材，最终需求必须重新经过资源、货币与原子库存预留校验；首尝试的 claim 不能冒充完整重试预算。随后才进入日时间/体力、机会成本和 fresh 终态回执。

## 历史记录 · 2026-09-13 显式目标日期处理提前量轴求值

- 新增 `acquisition_route_target_date_processing_lead_time.v1` 与 `build-acquisition-route-target-date-processing-lead-time`。构建器必须读取并逐对象重建完整库存预留报告，同时核对静态来源、同日透明快照和全部 route occurrence；聚焦 fixture 继续保留 `76/76`，未知来源类型不能默认为零等待。
- 本轴只回答“确定性产出等待是否允许该路线在目标日交付”。商店、钓鱼、地点采集等即时交互没有生产等待；交互耗时仍归 `daily_time_energy_budget`，钓鱼、挖掘、掉落等随机尝试仍归 `stochastic_retry_budget`，两者都没有被伪装成本轴成功。
- 作物路线复用反编译锁定的 `Data/Crops DaysInPhase`、最低产量/品质与透明桥逐格 `farm.crops/current_location.crops`。空耕地当天播种会保留权威基础生长期，但考虑肥料、技能和水田加速后只发布反编译可证明的“不早于次日”下限，因此可靠排除同日交付而不伪造精确成熟日；已有作物只有 `ready_for_harvest=true` 且可证明产出总量和最低品质覆盖完整需求才会同日通过。容量汇总声称存在目标作物但逐格状态缺失、数量不一致、产物投影非 exact 或生长倒计时矛盾时失败关闭。
- 蟹笼路线复用全局 `player.crab_pot_network`：一致的现成目标产物按实际堆叠与品质汇总为零日等待；已服务但尚未产出时依据锁定 `CrabPot.DayUpdate` 只证明最早次晨尝试，产物身份概率仍留给随机重试轴。机器、畜牧、鱼塘、果树、太阳能板和树液采集器等尚未穿过上游的生产类已显式登记，若提前到达本轴会失败关闭，不能偷用即时交互规则。
- 聚焦自测结果为 72 条上游不适用、2 条即时匹配、2 条新种作物确定性超期；成熟作物变体恢复为 4 条匹配，删除逐格作物状态后仅两条作物路线阻塞，篡改上游报告被拒绝。训练授权仍为 false；下一固定依赖轴为 `stochastic_retry_budget`，之后仍有日时间/体力、机会成本和 fresh 终态回执。

## 历史记录 · 2026-09-13 显式目标日期库存预留轴求值

- 新增 `acquisition_route_target_date_inventory_reservation.v1` 与 `build-acquisition-route-target-date-inventory-reservation`。构建器必须显式读取 `--strategy-ledger`，重新生成并逐对象核对完整货币预算报告，再保持每个 route occurrence 原样进入预留轴；缺账本、错误保存/玩家、旧或损坏契约不能退化为空账本。
- 每条已经通过资源与货币轴的路线独立生成确定性的原子 claim set，而不是把 1599 条替代路线同时写入账本。可消耗材料继续复用 `MaterialSupplyProjection`，按已授权节点、槽位和物品确定性拆分数量；`money`、`star_tokens`、`club_coins`、`qi_gems` 继续复用 `NativeCurrencySupplyProjection`。求值前扣除其他活动决策的预留，同一决策自己的 claim 则排除后重建并逐字段比对，以区分首次提案、精确已提交和需要整组替换。
- 全局预检查会把所有活动预留与当前透明供给重新投影；槽位消失、玩家漂移、货币域漂移、重复 ID、余额或堆叠超占都会使输入失败关闭。取消/完成记录保留审计但不占供给。已附着在鱼竿上的魔法鱼饵尚无现有账本可表达的嵌套槽位地址，因此该路线会显式阻塞，不能伪装成普通背包材料。
- 聚焦 fixture 保持 `76/76`：72 条继承不适用，4 条当时路线匹配；普通钓鱼无需 claim，两条作物分别提出 1 枚和 2 枚精确种子的槽位 claim，商店路线提出同一原子组中的 10 木材槽位 claim 与 200g claim，总计 3 个待提交组、3 个材料 claim、1 个货币 claim。其他路线占用 1 木材或 450g 时只让商店路线形成确定冲突；取消记录恢复供给，精确已提交组被幂等识别，错误玩家及全局超占账本被拒绝。
- 本轴只证明“若控制器选择该路线，这一整组当前资源能否被预留”，不负责替 Teacher 选择替代项，也不允许逐 claim 部分提交后执行。原子批量提交/路线组合属于后续控制器选择边界；固定下一依赖轴为 `processing_lead_time`，之后仍有随机重试、日时间/体力、机会成本和 fresh 终态回执。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 控制器原生货币预留基础设施

- `strategy_commitment_ledger.v1` 新增 `currency_reservations[]`，沿用材料预留的保存/玩家隔离、乐观 `ledger_revision`、取消状态与不可变历史。Backend 新增原生货币预留 upsert/cancel 接口；每次写入均从指定同日快照重算未预留余额，其他活动路线已占用的金额会先扣除，过量预留、旧修订和身份漂移均失败关闭。
- 预留域严格复用唯一的 `NativeShopCurrencies` 定义，只有 `0=money`、`1=star_tokens`、`2=club_coins`、`4=qi_gems`。透明桥、商店报价、目标日期预算轴和控制器供给投影不再各持一张映射表；读取还会核对 `shop_currency_balances` 的 money 行与 `player.money` 完全一致。
- 活动预留才减少可用余额，取消和已完成记录只保留审计；错误玩家、未知 ID/key、非正数、重复预留 ID、溢出和余额超占都会阻塞。Core game-free 36/36、Backend 189/189、Bootstrap Release 与 TransparentBridge E 盘真实游戏程序集构建均通过。
- 这是库存预留轴的原子账本前置条件。逐路线预留轴现已生成精确材料槽位/货币 claim，但仍不替 Teacher 同时选择全部替代路线；控制器选定路线后必须整组写入账本，后续候选再从扣除后的供给继续求值。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 显式目标日期货币预算轴求值

- 新增 `acquisition_route_target_date_currency_budget.v1` 与 `build-acquisition-route-target-date-currency-budget`。构建器会重新生成并逐对象核对完整资源输入报告，再把每个 occurrence 与原始 lowering 数量、静态来源和同日快照连接；全部 `33` 种来源类型必须进入唯一货币分类，未知类型不能静默视为免费。
- 透明桥新增 `player.shop_currency_balances.value`，严格按 1.6.15 反编译的 `ShopMenu.getPlayerCurrencyAmount` / `chargePlayer` 域读取 `0=money`、`1=star_tokens`、`2=club_coins`、`4=qi_gems`。玩家快照、全商店预览和已打开商店菜单共同复用这一个读取入口，不保留第二套货币映射。
- 商店路线以 `(shop_id, synced_key, qualified_item_id)` 绑定当前 `ShopBuilder.GetShopStock` 报价，价格、货币、有效交换物数量、库存与 `CanBuyItem` 都来自同一次原生投影。资源轴也改为复用该报价中的实际交换条件，因此物品查询动态改写交换物时不会与货币轴分叉；缺报价、售罄、不可购买或余额不足分别形成失败关闭或确定 miss。
- `native_money_payment` 使用 lowering 中的精确数量和 money 余额；当时权威库存中的四条 Vault 路线保持 2500/5000/10000/25000。商店路线按原生每次产出堆叠计算购买次数，再同步放大价格、有限库存和交换物数量；其余当时来源只有在上游资源匹配后才明确判为本轴无直接货币成本。
- 本轴只回答“一条路线在这张快照中能否支付”。跨路线重复花费、未来收入与资金预留由下一固定轴 `inventory_reservation` 处理；处理时长、随机重试、日时间/体力、机会成本和最终购买/付款回执仍属后续轴。聚焦 fixture 为 `76/76`、4 条活动路线匹配、3 条无需货币、72 条上游不适用；缺商店报价或缺余额只阻塞受影响购买，50g 对 200g 总价形成 1 条确定 miss。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 显式目标日期资源输入轴求值

- 新增 `acquisition_route_target_date_resource_inputs.v1` 与 `build-acquisition-route-target-date-resource-inputs`。构建器从同一输入重新生成并逐对象核对设施容量报告，再按路线 occurrence 对齐静态来源；全部 `33` 种来源类型必须进入唯一资源分类，未知类型和奖励、机器、畜牧、鱼塘、晶球、配方等尚未绑定输入的来源逐条失败关闭。
- 资源数量复用既有规范 `farm.material_inventory_graph.v1` 及 `MaterialSupplyProjection`，统计随身物品和获授权持久容器的立即可用节点，不另写一套背包/箱子汇总。材料图、鱼竿和蟹笼各自惰性读取并缓存，只有当天仍活动的对应路线才触达；共享或其他玩家资源继续默认不可花费，预留冲突仍由后续 `inventory_reservation` 轴持有。
- 当前纵向闭合：现存目标作物只按权威最低产量抵扣，剩余数量按最低单格产量向上取整为精确种子数；商店交易物按所需购买次数放大而金币/其他原生货币留给下一轴；普通目标日钓鱼无需耗材，只有所有保留窗口都要求魔法鱼饵时才核对可装饵鱼竿与已装/散装 `(O)908` 的合计数量；蟹笼现成产物、已装饵或主人 Luremaster 可通过，空未服务蟹笼在完整原生饵候选域接入前阻塞。
- 本轴只判定路线消耗或占用的输入。锄头、鱼竿、武器等可复用工具继续由既有候选/动作编译器 fresh 前置条件持有；设施建立、处理提前量、随机重试、时间体力和终端回执不在本轴内。这样不会把工具当作可消耗库存，也不会允许缺工具的动作通过执行门。
- 聚焦 fixture 保留 `76/76`：72 条上游不适用，2 条作物分别由 `(O)472 x1` 与 `(O)472 x2` 满足，1 条商店交易由规范图中的 `(O)388 x10` 满足，1 条普通钓鱼明确无需耗材。删除材料图只阻塞这 3 条输入路线；删除种子形成 2 条确定 miss。第 223 天旧快照保留全部 1599 条路线，197 条上游不适用、1402 条继承阻塞、0 条猜测通过。训练授权仍为 false；下一固定轴是 `currency_budget`。

## 历史记录 · 2026-09-13 显式目标日期设施容量轴求值

- 新增 `acquisition_route_target_date_facility_capacity.v1` 与 `build-acquisition-route-target-date-facility-capacity`。它从同一权威输入、快照和移动校准重新生成并逐对象核对上游地点报告，全部 `33` 种来源类型必须进入唯一容量分类表；未知类型不能静默当作“不需要设施”。
- 透明桥在已有低频 `social_route_date_evidence.v2` 每张地图行中加入紧凑 `prepared_cultivation_capacity.v1`：只遍历现有 HoeDirt 与无灌木花盆，完整记录开放槽、占用槽、花盆数和按收获物分组的占用槽。无法识别收获物身份的占用槽单独计数，既不冒充目标作物也不冒充确定空位。它不扫描或猜测尚未开垦的地块。
- 当前能通过前四轴的来源中，钓鱼、地点采集和商店明确为容量轴不适用；蟹笼复用地点轴已经证明的实际放置网络；作物按权威最低单格产量要求足够数量的现存目标作物槽与开放备耕槽，可跨全部匹配目标地图汇总。开放槽只满足容量轴，不证明种子、浇水、生长期或目标日收获；这些仍归资源和 processing lead time 等后续轴。
- 机器、鱼塘、动物、果树、树液采集与烹饪设施类已经显式分类为容量来源，但当前仍在更早的静态来源/地点轴阻塞。若其在相应原生绑定求值器完成前意外到达本轴，会逐条失败关闭，不会被“无设施要求”放行。
- 聚焦 fixture 保留 `76/76` 条路线：72 条上游静态不适用，4 条活动来源容量均匹配，其中 2 条作物绑定 Farm 的 2 个开放耕地槽、2 条普通来源无需容量。删除 Farm 容量字段只阻塞 2 条作物路线；完整零容量则形成 2 条已解析 miss 而不是缺证据。第 223 天旧快照保留全部 1599 条路线：197 条上游静态不适用，1402 条继承地点轴及更早阻塞，0 条用旧字段猜测容量。训练授权仍为 false。
- GoalConditionedBootstrap Release、TransparentBridge 真实程序集构建与聚焦自测通过，未启动游戏或训练。下一固定切片进入 `resource_inputs`；容量来源类仍须在更早来源/地点轴放行时补齐其精确原生绑定，不能由资源轴代偿。

## 历史记录 · 2026-09-13 显式目标日期地点路线轴求值

- 新增 `acquisition_route_target_date_location_route.v1` 与 `build-acquisition-route-target-date-location-route`。构建器会从同一组权威输入和同一快照重建并逐对象比较节日状态报告，再把当前适用来源绑定到目标地点，批量复用 Core 唯一的 `FutureRouteDateEvidenceProducer.ProduceLocationArrivals`。没有新增第二套路由或 BFS 实现。
- 地点绑定按来源保持原生身份：正常季节作物落到 `Farm`；反季节作物只接受实时 `SeedsIgnoreSeasonsHere()` 为真的地图；商店使用实时 `route_graph` 柜台端点，并只在静态地图资产名与实时地点 ID 精确相等时采用静态端点；蟹笼使用完整实时网络中的实际放置地点；`Default`、`Farm_<type>`、Data/Locations 精确地点和矿井覆盖分别按反编译规则处理。透明桥为此新增每张地图的 `location_context_id`、`seeds_ignore_seasons_here` 以及农场 `farm_type_key`，不从示例或地图名猜测。
- 路由必须有同日全地图静态可行走证据、完整连接器门禁和版本化保守移动校准。保证到达时间必须早于至少一个来源时段结束；受天气限制的来源按目标地图的实时天气上下文求值。该轴只证明“能在来源窗口内到达来源地图”，不宣称随机来源已出现、鱼点可用、商店有货、资源足够、终端交互成功或物品已取得。
- 聚焦 fixture 保留 `76/76` 条路线，目标日活动来源 `4/4` 匹配；锁门商店等待到 9:00 后于 9:02 到店。删去全地图路线证据后，72 条静态不适用路线仍保持已解析，4 条活动来源逐条阻断。第 223 天真实旧快照保留全部 1599 条路线：197 条静态窗口不适用、927 条继承上游阻断、475 条活动来源因旧快照缺路线日期证据和移动上下文而阻断，0 条被猜测放行。训练授权仍为 false。
- GoalConditionedBootstrap Release 构建与地点路线聚焦自测通过，未启动游戏或训练。下一固定切片进入 `facility_capacity`；随机、资源、预算、提前量和 fresh 回执继续由各自依赖轴独立持有。

## 历史记录 · 2026-09-13 显式目标日期节日状态轴求值

- 透明桥新增轻量 `world_progress.game_state_query_calendar_state`：实时读取 `Game1.stats.DaysPlayed`、当前总日数与时间、`Data/Festivals/FestivalDates` 的完整日期键、当前活动的被动节日 ID，以及 `Data/PassiveFestivals` 中每个条目的季节、起止日、开始时间与原始条件。它只遍历两个小型已加载数据表，不读取地图或执行节日逻辑。带地点上下文的普通节日形式仍明确标为 `not_projected`；当前权威 1599 条路线没有这种形式。
- 新增 `acquisition_route_target_date_festival_state.v1` 与 `build-acquisition-route-target-date-festival-state`。该阶段从同一原始输入和同一快照重新编译并逐字段比较上游解锁报告，随后按 1.6.15 反编译实现 `DAYS_PLAYED`、`IS_FESTIVAL_DAY` 和 `IS_PASSIVE_FESTIVAL_OPEN`，包括单重否定、普通节日日期偏移、被动节日开始时间的包含式边界，以及各原生处理器对尾随参数的真实行为。非法双重否定、缺字段和未投影地点上下文均失败关闭。
- 新报告不再复制一遍逐步膨胀的路线字段，而是把完整强类型 `upstream_route` 嵌入每条结果；fixture 和总回归均做逐对象 JSON 等值检查，避免后续轴再次丢失来源、窗口或解锁证据。锁定第 37 天的旧归档快照实跑保留全部 `1599` 条路线：`919` 条继承上游阻塞，`191` 条静态窗口不适用，`483` 条节日轴无需额外事实即可继续，剩余 `6` 条正是 Trout Derby、SquidFest 和普通节日条件，因旧快照早于新桥字段而明确阻塞。未知条件仍为 0，训练授权仍为 false。
- 聚焦 fixture 在被动节日开始时刻边界验证三类条件全部命中，并覆盖日期偏移、否定、地点上下文阻塞、缺字段逐路线封闭和上游制品篡改拒绝。GoalConditionedBootstrap 与 TransparentBridge Release 构建通过，未启动游戏或训练。下一固定切片进入 `location_route`；随机与资源条件继续由各自轴持有。

## 历史记录 · 2026-09-13 显式目标日期解锁状态轴求值

- 透明桥新增轻量 `world_progress.game_state_query_unlock_state`。它一次性读取 `Game1.getAllFarmers()` 中每位玩家的精确 ID、Current/Host 身份、三类邮件集合、完整 `Stats.Values`，以及该玩家队伍中仍为 `InProgress` 的特别订单 ID 和逗号拆分后的特别规则；同时读取 `IslandNorth.bridgeFixed`。字段来源逐项绑定锁定 1.6.15 的 `GameStateQuery.WithPlayer`、`PLAYER_HAS_MAIL`、`PLAYER_SPECIAL_ORDER_ACTIVE`、`PLAYER_SPECIAL_ORDER_RULE_ACTIVE`、`PLAYER_STAT` 与 `IS_ISLAND_NORTH_BRIDGE_FIXED` 反编译分支。该读取只遍历小型集合，不加载地图资产或生成全量世界快照副本。
- 新增 `acquisition_route_target_date_unlock_state.v1` 与 `build-acquisition-route-target-date-unlock-state`。构建器会从同一组权威输入重新编译目标日日历报告并逐字段比较，再要求透明快照的 `game_version` 和 `time.total_days` 与目标日一致。Current、Host、Any、All 和已存在的数值玩家 ID 按原生选择规则求值；Target 因来源调用上下文未被制品携带而明确阻塞，不会默认冒充 Current。
- 解锁轴只接收上述五类已反编译谓词。`IS_FESTIVAL_DAY`/`IS_PASSIVE_FESTIVAL_OPEN`/`DAYS_PLAYED` 仍归日历动态条件，`RANDOM`/`SYNCED_*` 归随机轴，`PLAYER_HAS_ITEM` 归资源轴，玩家地点谓词归位置轴；未知条件阻塞。邮件 `Any` 额外保留原生 `mailForTomorrow` 的 `%&NL&%` 后缀语义，玩家统计缺失键按原生 `Stats.Get` 返回 0，特别订单只认可 `InProgress` 行和精确逗号规则。每条结果继续无损携带上游 `source_resolution_status` 和命中的完整时间、天气及动态条件窗口，后续轴不需要回读或猜测另一份报告。
- 锁定 1.6.15 的旧归档快照（夏 10，`target_total_day=37`）实跑仍保留 `1599` 条 occurrence：`910` 条上游来源阻塞，`191` 条静态窗口不命中，`498` 条命中。命中项中 `489` 条无需解锁事实即可继续，`9` 条因旧快照尚无新桥字段而明确阻塞；另保留 6 个动态日历条件、1 个随机条件和 1 个资源条件，未知条件为 0。没有把旧快照中的零散相似字段拼成伪证据。
- fixture 已验证不同 Current/Host 玩家、Any/All/数值 ID、邮件类型与延迟邮件后缀、否定特别订单规则、统计包含区间、姜岛桥、Target 阻塞、缺字段按路线失败关闭、跨日快照拒绝和上游制品篡改拒绝。TransparentBridge 与 GoalConditionedBootstrap Release 均为 `0 warning / 0 error`，组合自测通过；未启动游戏或训练。其后续动态节日日历条件已由上节独立闭合，解锁轴本身不越权消费其他轴。

## 历史记录 · 2026-09-13 显式目标日期日历轴求值

- 新增 `acquisition_route_target_date_calendar.v1` 和 `build-acquisition-route-target-date-calendar`。输入为权威需求库存、路线 lowering、Master Angler 窗口、静态来源报告与显式 `target_total_day`；构建时重新运行静态来源编译并按完整类型化内容比对报告，任何中间制品篡改、来源哈希漂移、窗口结构错误或超出第三年爷爷评估前的日期都会失败关闭。
- 每个 occurrence 只会得到三种日历轴结果：静态窗口命中目标日、静态窗口不命中目标日、上游静态来源未解析。字段明确命名为 `static_window_matches_target_date`，不得解释为完整候选可用；命中的原始时段、天气、随机标志、位置要求和动态条件原样保留，其他 11 个依赖轴没有被隐式满足。
- 锁定 1.6.15 的春 1 日（`target_total_day=0`）实跑保留全部 `1599` 个 occurrence：`689` 条日历轴可判定，其中 `451` 条静态窗口命中、`238` 条不命中；`910` 条因来源解析器尚未覆盖而明确阻塞。15 条命中路线仍带动态条件。总报告保持 `training_label_eligible=false`，下游不得据此单轴结果发放标签。
- 聚焦 fixture 同时验证第一年春 1 日命中与第二年同日受 `!YEAR 2` 排除、包含式原生时间上界、动态条件保留、静态报告篡改拒绝和日期越界拒绝。Release 构建与聚焦自测通过；未启动游戏或训练。后续 `unlock_state` 轴已在上节实现；这里的 `calendar_axis_resolved` 只表示静态窗口可判定，仍携带的节日类动态条件必须在目标日快照上继续求值。

## 历史记录 · 2026-09-13 Data/Shops 库存与访问来源解析

- 权威需求库存新增并哈希绑定唯一 `runtime_data_shops`、`access_constraint_index`、`native_shop_stock_rule`、`native_shop_open_rule` 与 `native_shop_purchase_rule`。构建时核对锁定 1.6.15 的 `ShopBuilder.GetShopStock/CheckItemCondition/GetBasePrice`、`Utility.TryOpenShopMenu`、`ShopMenu` 扣款/交换/配方/同步库存/购买动作分支，以及原生 `GameStateQuery` 日期谓词；证据缺失、哈希漂移或源码标志漂移均失败关闭。
- `sells` 必须按精确 `shop:<shop id> / Data/Shops / payload.<shop id>.Items[<row>]` 绑定，并再次与权威访问索引的同一库存行及原生条件处理器核对。每条来源保留货币类型、价格、有限库存模式、交换物、配方、利润率/价格/库存修饰器需求、购买副作用、店主条件、地图柜台端点和可关联的门禁时间；完整原行由来源路径、文件哈希和行哈希共同固定。
- 商店日期投影按反编译语义求值 `SEASON`、`YEAR`（含否定）、`DAY_OF_WEEK`、`DAY_OF_MONTH` 与 `TIME`；玩家状态、同步选择和同步随机等条件原样留作目标日动态谓词。库存行的日期窗口不等于商店当日可进入：节日地图、店主在岗、NPC 日程、地图变化、实时剩余库存、价格/交换资源和原生菜单回执仍由下游轴逐项求值，所有行继续是 `target_date_pending`，`training_label_eligible=false`。
- 锁定 1.6.15 实跑保留全部 `1599` 个 occurrence，结果从 `580 resolved / 1019 blocked` 提升为 `689 resolved / 910 blocked`。`109/109 sells` 全部解析，覆盖 32 个商店；其中 68 条交换购买、69 条有限库存、1 条配方、2 条同步随机来源，原生条件处理器缺口和空窗口均为 0。27 条目标路线带直接柜台端点，22 条还能关联基础地图门禁；其余节日/动态入口明确留在位置访问轴，未被猜测补齐。
- 商店实现已拆为路由绑定、日历投影和访问证据三个文件；组合自测覆盖星期、否定年份、动态随机、交换/库存、端点/门禁以及 Data/Shops 与访问索引篡改失败关闭。Release 构建和精确数据制品已通过，`Run-Regression.ps1` 的新计数/字段断言与语法已更新。未启动游戏或正式训练。下一固定切片进入目标日期日历轴求值，不再继续添加静态来源家族。

## 历史记录 · 2026-09-13 Data/Crops 日历来源与生长约束

- 权威需求库存现在新增并哈希绑定唯一 `runtime_data_crops`、`native_crop_growth_rule` 与 `native_crop_planting_rule`。构建阶段同时检查锁定 1.6.15 `Crop.IsInSeason`、`Crop.DaysInPhase/RegrowDays`、野生种子随机分支，以及 `HoeDirt` 的季节许可和生长加速分支；缺文件、哈希漂移或源码标志漂移均失败关闭。
- `harvests_as` 按精确 `crop:<seed id> / Data/Crops / payload.<id>.HarvestItemId` 绑定。每条解析结果保留原生季节、逐阶段天数、基础首收天数、再生周期、浇水、稻田、纹理/精灵、种植地点规则和可能产物全集。正常季节生成 `crop_native_season` 窗口；非正常季节只生成要求地点实时证明 `seeds_ignore_seasons` 的窗口，不能把温室/姜岛许可外推到任意地块。
- 春夏秋冬野生种子 `495..498` 依据运行时行的 `TileSheets\\crops / SpriteIndex 23` 与反编译随机分支双重核对，公开完整可能产物域并标记 `stochastic_outcome=true`。`harvests_as` lowering 因此改为 `source_resolved_downstream`，不再把所有作物错误归为确定性 fresh receipt。
- 季节窗口只解决 `calendar_window` 的静态来源部分。肥料、农学家、稻田加速、实际浇水连续性、种子/地块/地点解锁、目标日首收或再生波次仍分别属于生长提前量和其他依赖轴；这些事实虽然已完整携带，尚未被求值，所有行继续是 `target_date_pending`，`training_label_eligible=false`。
- 锁定数据实跑保留全部 `1599` 个 occurrence，结果由 `505 resolved / 1094 blocked` 前进到 `580 resolved / 1019 blocked`；新增的 `75` 条均为真实作物路线，其中 `8` 条为野生种子随机来源。四集合自测、作物证据篡改负例、真实制品约束检查、GoalConditionedBootstrap Release、Core game-free `33/33` 和 Backend `188/188` 均通过；未启动游戏或训练。
- `Run-Regression.ps1` 已更新新计数和作物域断言且语法检查通过；完整脚本在本次逻辑执行前被既有 `OptionCapabilityRegistrySource.cs` 输入锁漂移拦截，工作树中的该文件未被本分支修改，本切片不擅自刷新无关权威锁。下一固定切片解析商店库存、开放与条件来源；随后才进入逐目标日期的动态依赖求值。

## 历史记录 · 2026-09-13 Data/Locations 日历来源解析

- 把 Master Angler 原有日历归一化逻辑抽成唯一 `NativeCalendarConstraintNormalizer`，Master Angler 与 Data/Locations 路线共同使用。原有 72 种鱼机会目录重建哈希保持 `fab38278265ca6019e8962b4cdfe4ff58897ae1dd7aa05de7ea7baff40c7fbcb`，没有形成第二套鱼类条件系统。
- 新解析器静态求交 `LOCATION_SEASON`、`SEASON`、`YEAR`、`TIME`、`WEATHER`；当前 1.6.15 Data/Locations 中已知但必须在目标日实时判断的邮件、节日、特别订单、地点、已持物、桥梁、累计天数和 `RANDOM` 等谓词原样保留为 dynamic conditions。任何未来出现的未知谓词进入 `unparsed_conditions` 并阻塞路线，不能静默放行。
- `acquisition_route_calendar_resolution.v1` 现在额外哈希绑定唯一 `runtime_data_locations` 证据，并按原始 `location/source row index` 精确连接考古点、地点采集和非鱼钓获。夹具刻意使用原始键名，大小写漂移、行号越界、未知条件、静态空窗口、过期窗口或证据哈希变化均失败关闭。
- 锁定 1.6.15 实跑仍完整保留 `1599` 个路线 occurrence：`505 resolved / 1094 explicit blocks`。已解析组成是蟹笼 `20`、Master Angler 地点钓鱼 `241`、矿井钓鱼覆盖 `3`、考古点 `68`、地点采集 `165`、非鱼钓获 `8`；所有 resolved 行均有至少一个窗口，当前精确地点数据不存在未知条件阻塞。剩余 `28` 种 route kind 继续统一标记 `blocked_pending_route_kind_calendar_parser`。
- 这批结果仍是 `resolved_static_source_window_target_date_pending`：动态谓词、目标日选择和其余 11 个依赖轴尚未求值，`training_label_eligible=false`，未启动游戏或正式训练。下一批先解析作物来源季节与生长期，再解析商店库存条件；生长/加工提前量仍归独立 `processing_lead_time` 轴，不能用来源季节替代。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，四收集组合自测（地点证据哈希、已知动态条件保留、未知条件阻塞及篡改负例）通过，Core game-free `33/33`，Backend `188/188`，`Run-Regression.ps1` 语法检查通过。

## 历史记录 · 2026-09-13 跨日期依赖轴防遗漏门禁

- 权威获取降层现在为每个 route kind 和每条 requirement route occurrence 固定列出同一组 12 个下游依赖轴：日历窗口、解锁状态、位置路线、设施容量、资源输入、货币预算、库存预留、加工提前量、随机重试预算、每日时间/体力预算、机会成本以及 fresh 终态回执。列表长度、唯一性或成员有任一偏差都会失败关闭。
- `GoalMethodFrontierBuilder` 把这份清单写入每个 `authoritative_acquisition_route` 节点，并明确标记 `dynamic_dependency_resolution_status=pending_per_route_axis_evidence`。这只证明依赖类别没有被遗漏，不表示这些依赖已经按具体日期求值，也不允许把“字段存在”冒充“路线当前可行”。
- 当前四收集 Teacher、全出货 Teacher 与统一目标前沿都拒绝旧的或被删轴的降层制品；总回归同时核对顶层、route kind 和全部 route occurrence，重复轴也不能绕过门禁。历史 `acquisition_route_option_lowering.v1` 若没有新清单会被视为 stale，必须从锁定权威输入重新生成。
- 离线验证已通过：GoalConditionedBootstrap Release 构建 `0 warning / 0 error`，四收集组合自测通过（包含删去一个轴后必须抛出 `InvalidDataException` 的负例），Core game-free `33/33`，Backend `188/188`。未启动游戏，未写正式训练数据，`formal_training_started=false`。
- 固定当时后续计划是实现逐路线、逐轴的动态证据解析，首先闭合 `calendar_window`，随后按解锁、资源/预算/预留、加工与随机重试、日内时间体力和 fresh 终态回执依次接入。只有 12/12 轴对某个候选均得到 `satisfied`、有证据的 `not_applicable` 或显式 `blocked`，该未来日期候选才可进入 Teacher 选择。

## 历史记录 · 2026-09-13 获取路线日历来源解析第一批

- 新增哈希绑定的 `acquisition_route_calendar_resolution.v1`，它逐条保留 requirement set、requirement、alternative、route 索引、物品和原始 source identity，并同时绑定需求库存、获取降层、Master Angler 窗口索引及机会目录。窗口还会从已验真的 catalog 确定性重编译并逐字段比较；任一旧降层、错库存、错窗口内容或被篡改的 catalog 哈希都会失败关闭。
- 没有新写第二套钓鱼日历：首批直接复用既有的反编译/运行时数据链 `MasterAnglerOpportunityCatalog -> MasterAnglerStageOneWindowIndex`，精确连接 `native_location_fish_spawn`、`native_mine_fishing_override` 和 `native_crab_pot_output`。窗口保留年份、季节、时段、天气、动态条件、地点、矿区、装备限制和随机结果标记。
- 锁定 1.6.15 全量制品实跑结果为 `1599 routes / 264 resolved static sources / 1335 explicit blocks`。已解析部分为蟹笼 `20`、地点钓鱼 `241`、矿井钓鱼覆盖 `3`；所有 resolved 行都至少携带一个精确窗口。
- 实跑额外识别出 `8` 条同属 `native_location_fish_spawn`、但目标物品不在 72 种鱼分母中的路线（例如钓点的木头、石头等非鱼产物）。它们没有套用任意鱼种窗口，而是保留为 `blocked_authoritative_fish_window_not_found`；总回归锁定这 8 个 occurrence identity，防止以后静默遗漏或误接。
- 当前报告状态仍为 `partial_static_sources_explicitly_blocked`，且每个已解析行仍标记 `target_date_pending`。静态来源窗口不等于某个目标日的动态条件已满足，更不等于其余 11 个依赖轴已满足；`training_label_eligible=false`，正式训练仍未启动。
- 离线验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，四收集组合自测及篡改哈希负例通过，Core game-free `33/33`，Backend `188/188`。下一批先把 Data/Locations 的非鱼钓点、采集物和蚯蚓点共用日历条件抽成单一原生条件解析器，再接作物季节/生长期和商店库存条件，避免按 route kind 复制解析逻辑。

## 历史记录 · 2026-09-13 博物馆与社区中心捐赠滚动路线

- `museum.donate_items` 与 `community_center.donate_bundle_items` 在目标建筑之外时，现按透明 `route_graph` 只发布当前地图可验证的首个 `route_connector_tile`；连接器携带精确物品、库存槽位以及博物馆/Bundle 身份。常规 daily-plan 可在 fresh 快照后锁定同一机械 continuation 续编；Teacher product 则把该连接器作为独立监督步骤，由外层 controller 在下一 episode 从 fresh 全量状态重建候选并选择终态捐赠。
- 社区中心 continuation 额外固定 `bundle_data_key`、ingredient 索引、需求数量和最低品质证据。Teacher frontier 只接纳身份、槽位、数量、品质均一致的路线步骤；负槽位、低品质或被篡改的 continuation 在上游失败关闭。
- 路线步骤使用 `authoritative_collection_rolling_route_step`，只证明声明的连接器端点确实抵达，不冒充捐赠完成。博物馆与社区中心的最终正样本仍分别要求逐项 `donated=false -> true` 和精确 Bundle ingredient `false -> true`。
- 目标地图内不新增独立站位动作：既有 `executor.donate_museum_item` 与 `executor.donate_community_center_item` 已负责本地 BFS、交互端点和原生菜单生命周期。这样避免与既有原生执行器形成两套实现，也不会把目标地图坐标提前绑定到来源地图快照。
- 结构化候选、续程锁定与 Teacher frontier 已通过隔离真实运行验收。隐藏、静音 E 盘运行 `runtime-collection-donation-routes-20260913-011522` 为 2/2：博物馆和社区中心均从 Town 的真实透明连接器出发，在 fresh 状态下续编既有原生捐赠器，连接器与终端原语全部 `applied/verified`，continuation 完成且精确终态变化成立。该运行使用 `--skip-training`，正式训练仍未解禁。
- 实测同时修正了原生 `WarpCommunityCenter` 特殊入口：透明桥现在按锁定版反编译公开 `CommunityCenter (32,23)` 目标和 `Game1.MasterPlayer` 的 `ccDoorUnlock || JojaMember` 门禁；执行器只通过原生 `GameLocation.checkAction` 触发，不直接传送。当时后续计划固定为跨日期调度，把日历、解锁、资源/库存预留和机会窗口接入同一 Teacher 依赖链。

## 历史记录 · 2026-09-11 有界多原语 Teacher 重复闭环

- 出货终端的“接近/投放”已收拢：同在农场但不处于出货箱站位时，`economy.ship_items` 现在一次编译为 `move_to_tile -> ship_inventory_item_to_bin`，两个原语共享精确物品 continuation，并只在原语之间 fresh 重绑定状态哈希。跨地图部分仍保持每次一个已解析连接器、抵达农场后再 fresh 编译上述本地有界队列，未把未来地图坐标提前绑定到旧快照。
- 隐藏、静音、隔离实测 `bounded-shipping-candidate-queue-20260911-221453` 连续执行 2 个 episode：第一轮机器收取按两原语完成；第二轮同一 `economy.ship_items` 候选按 `move_to_tile -> ship_inventory_item_to_bin` 完成，`queue_item_count=2`、回执 `ready`、需求转移 `1`，两轮均接纳。规范数据集为 `2 accepted / 0 rejected`，`formal_training_started=false`。
- `TeacherPreferenceQueueLoader` 现在接收 `1..8` 个有序 pending 队列项，仍要求每项恰好一个机械原语，并逐项核对所选 candidate、编译计划 step、来源 action、actor、执行模式和原始状态哈希；重复 queue-item/plan-step ID、越界队列或语义漂移均在派发前失败关闭。
- 运行器复用唯一的顺序队列执行链，不新增第二套执行器。每个后续原语派发前只把持久化命令的 `state_hash` 重绑定到上一原语的 fresh 后状态，其他命令语义保持不变；整队列回执记录原始编译哈希、逐步有效哈希、tick、changed facts、原语验证结果和严格的前后哈希链，且只有最后一步可以标记本次固定 Teacher 候选完成。
- `CurrentStageOneCollectionTeacherReceiptBuilder` 新增 `queue_execution_receipt.v1` 准入。它要求队列 ID、项顺序、原语、有效命令、逐步哈希/tick 链、fresh 状态和正向原生验证全部吻合，并在完整候选的权威 requirement credit 确实变化后才生成一条 `policy_decision_trajectory.v2`；旧单原语 `plan_execution_episode.v1` 路径继续兼容。
- 隐藏、静音、隔离实测 `bounded-stage-one-collection-teacher-20260911-211944` 连续执行 3 个 episode：第一轮同一 `farm.collect_machine_outputs` 候选按 `move_to_tile -> collect_machine_output` 两步收取 Raisins 并接纳；第二轮仅接近出货箱，需求未变化，因此保留执行证据但不写训练行；第三轮原生投放出货箱后才接纳。规范数据集结果为 `2 input / 2 accepted / 0 rejected / 0 duplicate / 0 conflict`。
- 控制器每次最多执行 16 个 episode，每个 episode 都从新的透明状态重新生成完整候选与独立 Teacher 偏好。中间 continuation 不会被误标为成功，真正的证据合同错误则立即停止。当前仍为 `formal_training_started=false`。
- 固定当时后续计划：在隔离真实游戏中验证博物馆与社区中心的远程滚动捐赠。跨地图只允许执行当前快照可验证的首个连接器；抵达目标地图后 fresh 续编既有原生捐赠 primitive，由该 primitive 自己完成本地 BFS 与菜单生命周期，不再生成重复的独立站位动作。完成捐赠回执验证后，再扩展跨日期候选与日历/解锁/资源约束，进入其余 17 个长期目标证明。

## 历史记录 · 2026-09-11 首条四收集 Teacher Product 真实轨迹

- 新增隔离、隐藏、静音的 `Invoke-CurrentStageOneCollectionTeacherProductRollout.ps1`。它从 E 盘源存档复制运行专用存档，只加载 TransparentBridge 与 RuntimeTestHarness，依次生成 fresh 全量快照、同状态候选排名、Master Angler 当前日期意图、独立 Teacher 偏好，并通过既有 Product Executor 执行偏好内嵌的精确队列；运行全过程保持 `--skip-training`。
- `TeacherPreferenceQueueLoader` 只接收 `training_singleplayer` 下恰好一个 pending 队列项和一个机械原语，严格核对状态哈希、actor、偏好、编译计划、来源 step 与队列。高层候选允许经唯一 `DailyPlanCompiler -> ActionQueueCompiler` 降为其执行器原语，但任意脱离所选编译 step 的替换队列都会在执行前被拒绝。
- 真实运行暴露并修复两项执行边界错误：独立移动不再把 `MaxCrops=16` 误当路线长度上限，`executor.move_to_tile` 显式携带 512 格上限；相邻机器候选不再生成零距离移动。Community Center 已完成 Bundle 计数也改为使用原生 required-slot 完成语义，而不是错误要求所有备选 ingredient 同时完成。
- 完整回归 `stage-one-collection-teacher-20260911-014412` 一次通过：原生碰撞移动实际走 41 格抵达 Dehydrator，随后 `executor.collect_machine_output` 收取 `(O)Raisins`；Product 请求哈希与执行前 fresh 哈希一致，回执为 `applied/verified`，并验证机器输出、库存及经验投影。
- 回执准入确认 `full_shipment:item:Raisins` 为精确库存 `0 -> 1`，生成首条 `policy_decision_trajectory.v2` Teacher 轨迹。标准 PolicyDataset 验证为 `1 input / 1 accepted / 0 rejected / 0 conflicts`。正向 primitive verification reasons 作为证据保留；只有 block/failure reasons 才拒绝准入。
- 本切片仍明确 `formal_training_started=false`，没有把单条证据混入正式训练组件。当时后续计划是建立有界重复采集控制器：优先支持同一候选的多原语整队列回执，在每个原语后 fresh 重绑定并仅在完整候选需求转移成功时写一条 Teacher 轨迹；随后扩大四收集候选种类与日期覆盖，再进入其余 17 个长期目标证明。

## 历史记录 · 2026-09-10 四收集集合 fresh 回执训练行准入

- 新增唯一 `CurrentStageOneCollectionTeacherReceiptBuilder`。调用方必须先落盘实际执行的 Teacher 偏好制品，再把同一制品的单个 pending 队列项、原生 `plan_execution_episode.v1` 回执和 fresh 后快照送入准入器；不得重新生成 queue ID 后拿新队列匹配旧回执。
- 偏好制品的五份源 SHA-256 必须仍与需求库存、获取降层、候选排名、前快照和 Master Angler 意图一致。准入器会重新计算 Teacher 选择与编译语义，核对选择、完整当时候选成员、顺序和去除随机 ID 后的队列内容；因此既保留实际执行 queue ID，也不信任可篡改的中间 JSON。
- 回执门严格绑定 run、queue、primitive option、唯一机械 step 的 primitive kind、前后 state hash、前后 tick、`applied/success/verified/fresh`、非空 changed facts 和完整 effective queue item。当前切片只接收恰好一个 pending 原语；多原语高层动作在拥有可靠的整队列完成回执前继续失败关闭。
- 每个 selected candidate credit 都必须由 fresh 后状态逐项证明：精确库存增加、原生 Full Shipment 完成或“库存减少并进入下一日原生结算”的 shipping-bin 增量、博物馆捐赠、Community Center 精确 ingredient 完成、鱼类收集完成，或 Master Angler 精确路线端点抵达。任一 credit 未变化都不生成训练行。
- 成功结果复用 `policy_decision_trajectory.v2`，只保留 Teacher 当前可用候选，清零 learner rank/score/model score/expected reward，并在 `audit.teacher_supervision` 中记录偏好、源输入、回执、后快照 SHA-256 及逐 credit 转移。既有数据集构建器已接收该行；错 queue 和“成功但需求未变”均被回归拒绝。
- 本切片仍明确 `formal_training_authorized=false`，总前沿仍为 `2/19 executable / 17/19 pending`。固定当时后续计划是把该准入器接入隔离的四集合 Teacher Product rollout，持久化同一偏好制品并取得第一条真实游戏回执训练行；之后再扩展可靠的多原语整候选回执、未来日期调度和其余长期目标证明。

## 历史记录 · 2026-09-10 四收集集合确定性 Teacher 偏好

- 新增唯一 `CurrentStageOneCollectionTeacherPreferenceBuilder`。它从需求库存、获取降层、同状态候选排名、透明快照和 Master Angler 目标日期意图重新构建四集合当前候选前沿，并逐一核对状态、目标、分母、候选 credit、选择组和五份输入 SHA-256；任一漂移均失败关闭。
- 固定字典序只使用权威当日截止、截止余量和时刻、终端需求转移、跨集合与逐需求精确 credit、候选稀缺度、确定性耗时和体力。Teacher 不读取 learner 的 rank、score、model score 或 expected reward；选中候选进入既有 `DailyPlanCompiler -> ActionQueueCompiler` 前，这些学习器信号会被清零。
- 候选 ID 只稳定报告顺序，不负责打破监督平局。两个最高候选的权威向量完全相同时返回 `blocked_authoritatively_tied_top_candidates`，不会按 ID 伪造偏好。当前不可用需求继续 deferred，未选中的当前可用候选只作为反事实比较项，不生成二元负标签。
- 结构化回归已证明：故意反转 learner 分数不会改变选择；精确平局会被拒绝；当日截止的 Master Angler 路线可经原有日计划和动作队列编译为一个 pending 项。测试夹具使用与透明桥同构的原生字段来源、碰撞网格和连接器，没有放宽生产执行门槛。
- 当时只形成单状态偏好标签，仍明确 `formal_training_authorized=false`，总前沿保持 `2/19 executable / 17/19 pending`。固定当时后续计划是把选中偏好绑定到 fresh 原生动作前后回执，只有精确 credited requirement 转移验证成功才生成训练行；之后仍需未来日期调度和剩余长期目标证明。

## 历史记录 · 2026-09-09 四收集集合统一实时候选合同

- 新增 `CurrentMasterAnglerTeacherFrontierBuilder`，把原生 72 种鱼的逐项完成态、权威需求库存、逐项获取降层、同状态候选排名和既有目标日期意图做严格连接。快照、库存、降层、排名、意图、窗口索引、机会目录和路线计时校准均保留 SHA-256 并交叉校验；旧状态或复制错配的意图失败关闭。
- Master Angler 当前候选统一经过 Core 的 `MasterAnglerCurrentCandidateMatcher`。路线连接、终端钓鱼和已就绪蟹笼必须携带精确目标意图、完整剩余路线加终端预留标记以及精确可能产物。错误鱼种的蟹笼候选不能仅靠附加合法意图参数获得绑定。
- 新增唯一 `CurrentStageOneCollectionTeacherFrontierBuilder`，统一 Full Shipment、Master Angler、Museum Collection 和标准 Community Center。一个实际候选只出现一次，但可同时获得它确实推进的所有需求 credit；社区中心 OR Bundle 保留剩余槽位上限。当前不可用需求继续 deferred，不生成负样本。
- 该产物只证明“候选属于当时可教集合”，还没有选出 Teacher 偏好。统一选择合同不读取 learner 排名或分数，不生成负标签，并明确记录 `teacher_preference_label_eligible=false`。因此总前沿仍为 `2/19 executable / 17/19 pending`，正式训练仍未解禁。
- 四集合结构化 fixture、聚焦 self-test、完整隔离 bootstrap 回归、Core game-free 测试和 Backend 测试均通过。历史 r36 快照缺少逐鱼透明分母，Master Angler 分支被正确拒绝，没有伪造标签。
- 固定当时后续计划：在这个统一当前候选集合上实现与 learner 无关的确定性 Teacher 偏好查询；随后仍需原生回执、未来日期调度和其余长期目标证明，才能开放正式全量训练。

## 历史记录 · 2026-09-09 博物馆与标准社区中心实时 Teacher 前沿

- 新增唯一 `CurrentCollectionTeacherFrontierBuilder`，复用 Full Shipment 已验证的当前候选门控、精确 `qualified_item_id` 绑定和权威获取端点证据。输入仍绑定需求库存、获取降层、同状态排名和实时快照四份 SHA-256；排名与快照 `state_hash` 不一致时失败关闭。
- 博物馆适配器要求 95 项 `donatable_items` 与权威分母逐项一致，并交叉检查 `donated_count`、`missing_item_count`、`missing_item_ids` 和 `collection_complete`。正标签只来自精确原生捐赠投影或对应缺件的权威获取端点。只有旧聚合计数、没有逐项分母的历史快照会把博物馆集合单独标为 blocked，不会猜测 95 项完成态。
- 标准社区中心适配器按 30 个权威 `bundle_data_key` 对齐运行时 Bundle 行，逐槽验证 OR 选择数、物品身份、数量、最低品质和完成位。每个未完成 Bundle 都公开 `remaining_slot_count`；物品备选要求“达到数量与最低品质前保留、随后原生捐赠”，金库要求“保留资金、随后原生付款”，不会把一个 OR Bundle 的全部备选误教成同时采购。
- 直接捐赠标签必须同时证明 Bundle、ingredient index、数量、品质、库存前后量和完成投影。获取标签必须有精确产物、正数量、权威 endpoint；最低品质大于 0 时还必须有一致的精确品质参数。未知品质、低品质和没有原生付款投影的金库候选均 deferred，不生成负标签。
- 结构化回归得到 4 种合法绑定：博物馆捐赠、博物馆获取、Bundle 捐赠、高品质 Bundle 获取；不完整品质与金库伪候选均被排除。真实 r36 为 `museum blocked (legacy per-item denominator absent) / community center 30 observed / 0 current binding`，准确保持不可训练。当时后续计划把现有 Master Angler 日期窗口意图接入同一实时需求前沿，并统一四个收集集合的候选选择合同；正式训练仍未解禁。

## 历史记录 · 2026-09-09 全出货实时 Teacher 前沿

- 新增唯一 `CurrentFullShipmentTeacherFrontierBuilder`，把实时快照、同状态排名、154 项权威需求库存和逐需求获取降层做严格交集。输入文件均记录 SHA-256，排名 `state_hash` 必须与快照完全一致；透明进度的分母、逐项身份、已出货计数、缺失列表、完成标志和比例必须彼此一致，否则失败关闭。
- 正标签只有两种：带完整原生 Full Shipment 贡献字段的当前出货候选，或产物 `qualified_item_id` 精确匹配缺失物品且 option 是该物品权威获取路线端点的当前候选。已完成物品、同 option 的错误产物、被门控候选和没有精确依赖绑定的 supporting option 均不得进入。
- 没有当前候选的需求被标为 deferred，不生成负标签。真实 r36 快照验证为 `154 required / 6 completed / 148 missing / 0 current exact binding`，因此报告正确保持 `training_label_eligible=false`，没有把候选池暂时未出现的 148 项错误训练成负例。
- 该切片只闭合 Full Shipment 的“实时缺项 -> 当时具体动作”证据边界，没有改变总前沿 `2/19 executable / 17/19 pending`，也没有启动正式训练。博物馆与标准社区中心的同类实时适配现已由后续切片完成；当时当时后续计划是接入 Master Angler 已有的日期窗口意图。

## 历史记录 · 2026-09-09 Teacher 逐需求获取绑定

- 获取降层不再只报告 `33/33` 路线种类总数。现有唯一
  `AcquisitionRouteOptionLoweringBuilder` 为 4 个 Stage 1 收集分母、351 个需求组、450 个
  具体备选物品保留全部 1599 条权威来源，并逐条绑定 supervision/uncertainty、端点高层选项、
  支持选项及 Teacher/runtime 准入状态。
- Goal-Method 超图直接消费同一报告，形成
  `existing_high_level_option -> authoritative_acquisition_route ->
  authoritative_requirement_alternative -> authoritative_requirement -> requirement_set -> root method`
  的可追溯链。四个收集方向均显示自己的分母和准入计数，不能再用全局 33 类路线替代逐物品证明。
- `complete_full_shipment` 中声称树苔路线缺失的旧阻塞已删除；树苔、野树、春葱和文物点都由当前
  获取报告按具体物品绑定。剩余阻塞准确限定为实时日历/解锁/资源/路线时间/概率重试与一件预留，
  以及 154 项跨日结算和第三年前完成证明。
- 本切片没有把 `2/19` 改写为更高数字。它闭合的是 Teacher 从“缺少哪件物品”定位到现有高层
  获取动作所需的静态连接；跨日/跨季调度和原生新存档证明仍未完成，正式训练继续禁入。
- `current-option-input-lock.v1.json` 的仓库文本源改为显式 `utf8_lf` 规范化哈希，修复新检出工作树
  因 CRLF 与历史混合换行产生的假漂移。外部知识和反编译输入仍保持原始字节锁。

## 历史记录 · 2026-09-08 当时权威检查点：EVD-335

- `foraging.harvest_spring_onions` 已闭合读、候选、编译、原生运行和输出五门。它只接受当前已加载地图中原版基类 `Crop` 的 `forageCrop=true / whichForageCrop=1`，并复用唯一 `harvest_crop_tile -> executor.harvest_crop` 生产链；普通作物、姜和自定义作物不会进入该候选。
- 本步真实运行发现并修复了透明桥缺口：原生春葱的 `indexOfHarvest` 为空，`Crop.harvest` 才按觅食 ID 动态创建 `(O)399`。桥现在只对精确原版春葱发布 `harvest_item_projection_status=exact_from_decompiled_native_spring_onion_branch`，未知分支继续失败关闭；必需事实门也已授权该专用 adapter。
- 隐藏静音 E 盘隔离运行 `artifacts/runtime-spring-onion-daily-plan/runtime-spring-onion-daily-plan-20260908-233958/summary.json` 通过：DailyPlan 选择 `harvest:Farm:64,15`，原生执行 `applied/verified`，库存 `(O)399` 为 `0 -> 1`，Foraging XP 为 `19376 -> 19379`，目标作物被移除且状态哈希变化。运行使用 `--skip-training`，不得作为正式 Teacher 轨迹。
- 权威生成结果为 `230 registered / 232 semantic / 229 compiler-bound / 153 runtime-verified / 64 training-eligible / 145 Product Executor / 2 catalogued blocked`；获取路由为 `30/33 admitted / 3 blocked / 1599 occurrences`。剩余路由是树苔、砍野树产物和砍野树种子掉落，后两者共享同一个高层砍树获取缺口。
- Teacher 前沿仍为 `2/19 executable / 17/19 dependency pending / 0 missing graph / 0 governance blocked`，所以正式全量训练仍禁入。直接当时后续计划是闭合 `native_tree_moss_harvest` 的单一高层选项，并复用已验证的野树交互/收获内核，不创建第二套树木执行系统。

## 历史记录 · 2026-09-08 当时权威检查点：EVD-334

- `foraging.excavate_artifact_spots` 已用 E 盘隔离存档完成隐藏静音高层运行验收。唯一 `(O)590` 候选从 `foraging.excavate_artifact_spots` 经 DailyPlan 降为 `clear_obstacle_tile -> executor.clear_obstacle`，没有新增第二套挖掘执行器。
- 原生 Hoe 回执验证目标移除、完整输出 unit-state 集合、`+15` Foraging XP、`ArtifactSpotsDug`、地形与 Defense Book 邮件状态，并取得新鲜且变化后的快照哈希。`(O)SeedSpot` 与其他可清理物仍在上游排除；运行使用 `--skip-training`，夹具校准行不得冒充正式 Teacher 数据。
- 权威生成结果为 `229 registered / 152 runtime-verified / 63 training-eligible`；获取路由为 `29/33 admitted / 4 blocked / 1599 occurrences`。剩余四类是春葱收获、树苔收获、砍野树产物和砍野树种子掉落。
- Teacher 前沿仍为 `2/19 executable / 17/19 dependency pending / 0 missing graph / 0 governance blocked`。本步消除了多个终局共享的获取叶缺口，但尚未完成相应日历、解锁、资源、路线、保留量、概率重试和新存档期限证明，因此正式全量训练仍禁入。
- 直接当时后续计划：为 `native_spring_onion_harvest` 注册并闭合单一高层采集选项，复用现有 `executor.harvest_crop`，不得把机械原语直接作为 Teacher 输出。

## 历史记录 · 2026-09-08 Teacher / Student 收敛合同与当时禁入状态

- issue #90 与 #91 内容完全重复；其中 Teacher bootstrap、DAgger 重标、native outcome 后期优化和运行时 Student 主决策的方向正确，已整理为唯一规范 `docs/TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`。
- 系统目标是可验收的工程收敛，不宣称神经策略的数学全局最优。有限 Teacher 必须同输入同输出并终止；Student 以独立 Teacher 分歧、learner-state 恢复、新存档 21/21、零硬约束违规和性能门冻结。
- 当时并未收敛：Teacher 前沿为 2/19 executable、17/19 expansion pending，但 19/19 已具有可执行叶或类型化依赖图，不再有缺图 criterion。Master Angler 仅闭合当时日期的全 connector 路径和终端时间储备；未来日期、主动蟹笼容量、概率重试和真实跨日回执仍未完成。
- 代码审计确认 `StructuredPolicyTrainer.BuildPairs` 仍以 `candidate.Selected` 选正例。正式全量训练继续禁用；旧 r24-r35 只保留为控制面、执行器、恢复和性能证据。后续必须先类型化 `teacher_preference`、`native_outcome`、`student_observation` 并实现 learner-state Teacher 重标。
- 本块离线验证基线：Core `2467/2467`、KnowledgeCompiler `585/585` blocking 0、option matrix `228 registered / 62 training-eligible / 151 runtime-verified`，isolated full regression 和 Master Angler late-arrival/no-terminal-reserve 回归通过。没有启动游戏。

## 历史记录 · 2026-09-05 当时权威检查点：r35 round01

- `train.server.20260905.r34.plan05` 从 Summer 9 精确基线启动后，暴露了卧室窄通道中的真实执行缺陷：摸完宠物后，通用移动会为占路宠物反复规划不存在的替代路线，却不继续发送原生移动输入，最终六次命中 `movement_soft_obstacle_timeout`。该轮只有 1/2 个主动作验证，未进入跨日边界，事务保持 `staged_not_committed`；Summer 9 存档和五项 canonical 哈希均未改变，失败证据及 6 份执行器诊断已归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round05-failed-20260905-112856`，远端/本机 130 / 130 且哈希差异为 0。
- `20fff604434584f966824e043360b1802647027d` 保留唯一通用移动链：动态角色阻挡时先按 soft-obstacle BFS 重规划；无替代路线时持续发送原生方向输入，让宠物/NPC 按游戏机制让路；180 tick 超时仍失败关闭。RuntimeTestHarness Release 构建 0 warning / 0 error，game-free governance 20/20；`formal-r35-20fff60-20260905` 的 367 项发布清单通过校验。
- `train.server.20260905.r35.plan01` 从未改变的 Summer 9 基线重跑。日志记录角色从 FarmHouse `(10,9)` 开始移动并在 2 秒后原生切换到 Farm，旧软障碍超时未复现；5 次迭代完成 2 个 verified 主决策和 3 个原生保存边界尝试，进入 Summer 10。新增 3 条 applied 高层轨迹：每日摸宠物、完整处理 `passedOut3_Billed 8` 邮件、完整处理 `spring_23` 邮件；控制面回屋/睡眠仍为 0 条策略轨迹。
- 事务为 `committed_after_native_save_boundary`，存档树 SHA-256 从 `c542c7c9ed8192c2e26b19cc714c94e9589dad27ea586efe800e6208a6fa7756` 变为 `19de382994da925d5a47707eb143a89f0710a38e89dec7374b4d217faaaf62d7`。canonical 更新为 accepted 221 / rejected 0、163 / 5 / 53、5094 pairs，执行特征 230 行、horizon 观测 8 行；checkpoint / manifest SHA-256 为 `dcb6ff2bc8dd28a223fcd0fe5a27c4a58215ff23084b6013a30f9299daac774b` / `dc7628a1add5a9db7247980bc7e19e4f671c93c3b0f51bcf12e4917e3085904b`。
- unresolved Product pending 为 0，resolved 为 13；正式容器 `exit=0 / OOM=false`。完整成功归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r35-round01-20260905-120836`，远端/本机均为 165 个文件，SHA-256 不一致为 0。
- 后续唯一合法起点是 Summer 10 存档与上述 canonical。服务器正式训练进程已停止；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round04

- `train.server.20260905.r34.plan04` 从 round03 已提交的 Summer 8、存档和 canonical 精确哈希准入；并发 1，5 次迭代包括 2 次主决策和 3 次原生保存边界尝试，最终进入 Summer 9。存档树 SHA-256 从 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f` 变为 `c542c7c9ed8192c2e26b19cc714c94e9589dad27ea586efe800e6208a6fa7756`。
- 新增 3 条完整策略轨迹：`mail.process_letter:route:fertilizers:FarmHouse:3,12`、每日摸宠物和 `mail.process_letter:open:spring_19_1`，最终 primitive 分别闭合到 `close_menu`、`pet_interact`、`close_menu`，均为 applied。另一个未完整闭合的队列尾项只记录 skip；`recovery:native_save_boundary` 的控制面回屋/睡眠仍为 0 条策略轨迹。
- canonical 更新为 accepted 218 / rejected 0、160 / 5 / 53、5011 pairs；执行特征从 224 增至 227，horizon 观测保持 8。checkpoint `structured-policy-775fb64e3351349790e74543` 的 SHA-256 为 `0b31d8084c6fcc728defd4ba916d8542b2e1efb13cbf97695f1db94a910b1035`，manifest SHA-256 为 `0af71721588d0da1bd78cb72cfca6e3cd74e7545f2edb2726390d3d9b08cf96b`；train/validation/test pair accuracy 为 0.992217 / 1.0 / 0.965602。
- 两次主排序耗时 2.228 秒和 82.095 秒；三个控制面边界中的后两个排序为 138.9 毫秒和 9.5 毫秒。原生日志记录 `SaveGame.Save() completed without exceptions.` 及 `starting summer 9 Y1`；unresolved Product pending 为 0，正式容器 exit 0、OOM false。
- 完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round04-20260905-111352`，远端/本机均为 172 个文件，缺失、额外、SHA-256 不一致均为 0；本机按源码算法重算的存档树摘要与报告一致。
- 后续唯一合法起点是 Summer 9 存档与上述 canonical；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round03

- `train.server.20260905.r34.plan03` 从 round02 已提交的 Summer 7、存档和 canonical 精确哈希准入；并发 1，4 次迭代包括 2 次主决策和 2 次原生保存边界尝试，最终进入 Summer 8。存档树 SHA-256 从 `e967b3990b683b78a9b6bcad59fcfa0457907cf8e7f7b13ce5829c05bf54397a` 变为 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f`。
- 两个主候选分别为 `ship:Farm:71,14:0:311:route:FarmHouse:3,12` 和 `mail.process_letter:approach:fertilizers`。前者完成原生出货，后者完成到邮箱的移动，机械回执均为 applied/verified；但两条 selected queue candidate 都没有完整闭合，所以只记录执行特征和 horizon 证据，没有伪造高层策略轨迹。`recovery:native_save_boundary` 的回屋与睡眠继续只属于控制面。
- canonical 策略数据保持 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；执行特征从 220 增至 224，horizon 观测从 7 增至 8。checkpoint `structured-policy-1946ee6d8c59b1a72f692a50` 的 SHA-256 为 `64283a58cb8e48eed06baffb1c8116a241033c26b00e792ec01807db537c345c`，manifest SHA-256 为 `47ac7aaff2798f3546eea1e0a5772a524dc3d9ee959e4f0506136d7e3fb18a78`；train/validation/test pair accuracy 为 0.992276 / 1.0 / 0.96683。
- 两次主排序耗时 2.427 秒和 45.624 秒；两个控制面边界排序为 203.5 毫秒和 12.4 毫秒。原生日志记录 `SaveGame.Save() completed without exceptions.` 及 `starting summer 8 Y1`；unresolved Product pending 为 0，正式容器 exit 0、OOM false。
- 完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round03-20260905-085309`，远端/本机均为 119 个文件，缺失、额外、SHA-256 不一致均为 0；本机按源码算法重算的存档树摘要也与报告一致。
- 本轮目标已经完成并停止。后续若继续，唯一合法起点是 Summer 8 存档与上述 canonical；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round02

- `train.server.20260905.r34.plan02` 从 round01 已提交的 Summer 6 和 canonical 精确哈希直接准入；并发 1，5 次迭代包括 2 次主决策和 3 次原生保存边界尝试，最终进入 Summer 7。存档 SHA-256 从 `08b6e014081653f6625ad57106624417e0e02ca5f5778d744a393911f7dc6a49` 变为 `e967b3990b683b78a9b6bcad59fcfa0457907cf8e7f7b13ce5829c05bf54397a`。
- 4 条新增策略轨迹依次为完整处理两封邮件、照料宠物和收取树产品，均为 applied。邮件候选 ID 中的 route/open 只表示进入阶段；第一封邮件在同一 lease 内连续完成 `traverse_connector → move_to_tile → interact → wait_ticks → close_menu` 后才入库，不是机械步骤独立训练。队列中 fresh 后不可用的社交尾项被拒绝并释放旧 lease，只记 `selected_queue_candidate_not_fully_verified` skip；没有伪造成功轨迹，`recovery.stabilize_day` 入库数仍为 0。
- 两次主排序耗时 2.412 秒和 43.828 秒；三次控制面边界中的后两次排序为 154.6 毫秒和 22.2 毫秒。没有回到 r33 的数分钟重复候选构建。
- 事务为 `committed_after_native_save_boundary`；canonical 为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs。checkpoint `structured-policy-bd3f223f2cdda380168406bc` 的 SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4`，manifest SHA-256 为 `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`；validation/test pair accuracy 为 1.0 / 0.96683。
- 原生日志记录 `SaveGame.Save() completed without exceptions.`；unresolved Product pending 为 0，容器 exit 0、OOM false。完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round02-20260905-043913`，远端/本机 170 / 170，缺失、额外、SHA-256 不一致均为 0。
- 直接当时后续计划：以 Summer 7 存档和当前 canonical 哈希创建下一份有界计划，继续扩大真实日序列；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round01

- r33 round10 已从 Summer 4 推进到 Summer 5，完成 2 个 verified 主决策和 3 个原生存档边界迭代，事务正常提交；它证明 r33 修正后的 canonical 可以连续第三次进入下一轮。随后 `10b7722` 在不删减 full snapshot 字段的前提下，将同一 `SnapshotEnvelope` 的完整 route connector 候选构建结果缓存，并从缓存按调用上限截取。
- 默认自主候选集合现在只有一个权威入口；显式目标候选（尤其 `exploration.visit_location`）、校准候选和玩家指令候选不会被空请求擅自纳入。`recovery.stabilize_day` 仅显式加入控制面边界，未写入训练轨迹。
- 冷启动 Farm full snapshot 默认排序由约 227.9 秒降至 28.266 秒；119 上 `train.server.20260905.r34.plan01` 的室内首次排序为 1.918 秒、室外首次排序为 43.329 秒，随后边界排序为 145.6 毫秒和 9.7 毫秒。透明字段未裁剪，性能收益来自候选派生复用。
- r34 使用 `formal-r34-10b7722-20260905`、并发 1，4 次迭代完成 2 个主决策和 2 个原生存档边界，产生 `mail.process_letter`、`economy.ship_items`、`mail.process_letter`、`farm.care_for_pets` 四条 applied 策略轨迹。Summer 5 → Summer 6；存档 SHA-256 从 `09bf903b36e1642339784f21a7331d309511280cb96abf458f7f7d8dca86c9a2` 变为 `08b6e014081653f6625ad57106624417e0e02ca5f5778d744a393911f7dc6a49`。
- 事务状态为 `committed_after_native_save_boundary`；canonical 为 accepted 211 / rejected 0、train / validation / test = 153 / 5 / 53、4783 pairs。checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`，manifest 的 input、horizon、cleaned 和三分区摘要均已复核。
- 完整制品归档在 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round01-20260905-041036`；远端/本机 189 / 189，缺失、额外、SHA-256 不一致均为 0。正式容器以 exit 0、OOM false 退出，只保留独立观察隧道。
- 直接当时后续计划：从 Summer 6 存档和当前 canonical 哈希创建 r34 round02 有界计划，继续真实 Product rollout。跨季、跨年、Grandpa 21 和 Companion 适配尚未验收，不得称为全量训练完成。

## 历史记录 · 2026-09-02 issue #89 运行证据新鲜度与 CI 修正

- `native_object_execution.v2` 现由 `RuntimeEvidenceCatalogSource` 冻结运行路径修订、32 个源文件的规范化 SHA-256，以及六个 EVD-271/272/274/276/278/279 运行证据对应的 artifact/source/build identity 和三份精确 DLL SHA-256。未知证据、修订变化、源码漂移或 DLL 身份缺失一律按 stale 关闭 Runtime/Output 准入，不能再因 evidence id 非空而继承 `RuntimeVerified`。
- 本轮只强绑定 #88 实际重跑的六个动作：旋转屋内植物、演奏 Singing Stone、收取 Slime Ball、从料斗取草、收取 Auto-Grabber、使用 Mini-Obelisk。其他历史动作暂为 `LegacyUnbound`，保留原行为但不冒充已完成强绑定；后续必须按实际运行产物逐域迁移。
- capability 输出新增 `runtime_evidence_freshness`、`expected_runtime_path_revision`、`runtime_path_source_sha256` 和 `runtime_evidence_bindings`，供训练准入与审计直接追溯。
- GitHub Actions 新增不依赖 Stardew 私有游戏 DLL 的 Core governance profile，覆盖原生对象机械治理和证据新鲜度测试。此次本机结果：governance `16/16`、Core `2252/2252`、Backend `171/171`、Release 构建 `0 warnings / 0 errors`。
- issue #86 的 typed payload v2 经复核属于增量架构迁移，不是本轮阻断 bug；Transfer/Place/Craft/Quest 等对象化扩展仍按原计划推进，不与 #89 修复混写。服务器本轮只更新静态发布物，游戏、Backend、Product Executor 和训练循环均保持停止。

## 历史记录 · 2026-09-01 当时接续点：r29 有序高层队列边界闭环通过

- 训练根已先迁移到 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`；远端清单 910 个文件、本地 910 个文件，逐文件 SHA-256 验证 910/910，通过且远端源目录未改动。
- 唯一有效决策边界现为：模型/策略排序器一次选择一条有序高层候选队列；运行时持有该队列及游标，依次在每个候选边界读取 fresh snapshot、重新物化并由 C# 确定性编译。候选内部的寻路、移动、交互、等待、关菜单等 continuation 也只做确定性重编译，不重新调用模型。
- 只有整条队列完成，或当前候选因时间、能量、资源、身份漂移、阻塞或回执不确定而使队列失效时，才允许重新调用策略排序器。队列间先后关系在每个候选边界重新验证；失败候选不得计为完成，也不得越过 continuation 执行下一个候选。
- 119 上 `formal-r29-20260901 / train.server.20260901.r29` 的有界证明为：1 次策略排序选择 4 个高层候选，执行 9 个机械动作；发生 3 次候选边界刷新和 5 次候选内部 continuation 刷新，均未调用策略模型。4 个高层候选各形成 1 条成功策略轨迹，最终 `selected_queue_decision_complete=true`，无跳候选、无阻塞入库。
- r27/r28 是用于暴露错误边界的受控回归探针：分别发现了装箱站位被误纳入高层语义身份、以及 continuation 尚未结束却跨入下一候选的问题；两者均已在 r29 前修正，不得作为成功训练证据。下方 r24/r25 中“每次 fresh replan 形成独立策略轨迹”以及“每个外层 iteration 只能容纳一个高层目标”的解释已被本节取代，只保留为历史诊断记录。
- 当前并非多日全量训练完成。服务器约剩 4.2 GB，继续长训前仍须扩容或迁移训练根；之后先做有界多外层迭代恢复验证，再启动可恢复的多日正式批次，并同时监控策略调用次数、候选边界编译次数、UPS、磁盘和 Product 回执。

## 历史记录 · 2026-09-01 当时接续点：r25 四轮滚动证据通过

- 正式训练请求与 `training_run_manifest.v2` 新增 `max_persisted_iterations`，默认并冻结为 4；launcher、
  Windows/Linux 启动器和服务器 Compose 已贯通。只滚动当前 run 的旧 `live-snapshots` 迭代家族，
  不删除策略轨迹、Product 回执、数据集、checkpoint、manifest 或日志。
- 119 已部署 `formal-r25-20260901`。`train.server.20260901.r25` 的 manifest 实际记录
  `max_attempts=1 / max_persisted_iterations=4 / min_free_space_mb=4800`；邮件五步再次全部
  `applied/verified/fresh`，5 条显式候选轨迹全部入库，来源遗漏为 0。
- 数据集现为 accepted 186 / rejected 0 / train 128 / validation 5 / test 53，首次出现非空 validation；
  checkpoint 为 `structured-policy-52c9f785cc6dcc46c02f94e7`，SHA-256
  `b97bbdc1b64ba77b38097fc691581d4397c32807246f312b00dd883249e23b67`。
- 训练后约 54.124 UPS，full snapshot 12 次、平均 1406.835 ms。服务器只有一块 60GB 系统盘，约
  4999 MiB 可用、使用率 92%。滚动窗口降低未来单 run 增长，但当前仍无第二数据盘；不得开启无界长训。
  下一外部准入是扩容/挂载训练盘，或经审核回收明确可删除的旧服务器资产；在此之前只允许有界批次。

## 历史记录 · 2026-09-01 当时接续点：r24 单目标正式闭环通过，准备多日长训

- 119 服务器已部署 `formal-r24-20260901`。运行 `train.server.20260901.r24` 在同一隔离存档上完成一个
  邮件高层目标 episode，内部原生执行 `traverse_connector -> move_to_tile -> interact -> wait_ticks -> close_menu`；
  五步全部 `applied/verified/fresh`，最后以 `objective_continuation_completed_iteration_boundary` 停止，
  没有在同一外层迭代误跑第二个目标。
- 训练单位已固定：外层 iteration 是模型级目标，不是机械动作数；机械动作完整保存在 execution episode。
  每次 fresh replan 中被明确选中的高层 continuation 候选各形成一条策略轨迹，纯编译器展开的按键/寻路
  原语不伪装成模型决策。r24 补齐三个邮件阶段的 `candidate_id` 后，本轮生成 5 条成功 Product 轨迹，
  `effective_candidate_id_missing=0`。
- 正式数据从 accepted 176 增至 181，rejected 仍为 0；train/validation/test 为 128/0/53，train pairs
  为 3415。当前 checkpoint 是 `structured-policy-35eb3439e19036a75b9c628b`，SHA-256 为
  `11f38de448370bb401278bd8cd32ca4faea1e42e67df3b5299e20394c53ef0f7`。
- 低频观察下实测约 56.346 UPS，full snapshot 12 次、平均 1430.682 ms，未形成持续卡顿。游戏、Backend
  和 Product Executor 仍运行，LiveTrainingLoop 已正常退出，因此现在不是后台长训状态。
- 正式 Product 控制闭环已经成立并产生真实 checkpoint 更新，但“全量训练完成”仍未成立：尚未连续覆盖
  完整游戏日、季节、年份和第三年爷爷 21 分目标，也没有足量 validation 数据。服务器仅约 5072 MiB 可用、
  使用率 92%；当时后续计划先扩容或迁移 `/state/formal-training-r18`，复核全量哈希后按有界、可恢复的多日批次
  启动长训。当前 C# 线性 ranker 不需要 RTX 5070 8GB，GPU 留给后续神经模型阶段。

## 历史记录 · 2026-09-01 当时接续点：EVD-326 正式训练编排与真实结构化更新

- Product Executor 轨迹已从测试 Harness 版本解绑：正式回执写入 `executor=product_executor.v1`，清洗器可识别历史 Harness 校准数据，但 `StructuredPolicyTrainer` 与 checkpoint 只接受 Product 版本。当前权威轨迹 schema 是 `policy_decision_trajectory.v2`，旧文档中的 `.v1` 表述不得继续沿用。
- `training_run_manifest.v2` 冻结结构化 checkpoint、policy dataset manifest 及其 SHA-256，并记录 Product Executor、游戏和 LiveTrainingLoop 三个进程。正式 launch 必须复用同一个已成功 prepare 的 manifest；任一工具缺失、路径越界、hash 漂移、非 SMAPI、非隐藏静音或非 loopback 执行端都会失败关闭。启动任一后续进程失败时，已启动进程会回收，避免半闭环残留。
- `training_ready_probe.v2` 现在验证透明快照/run-id、游戏与两个训练进程、Product `/health`、收据目录、孤立 pending、数据 manifest/checkpoint 及全部 cleaned/train/validation/test 文件 hash。pending 不会重发；探针报告 `formal_product_receipt_recovery_required`，由同请求恢复为 indeterminate final 后重规划。
- 正式 LiveTrainingLoop 不再训练旧 baseline 冒充小模型更新。每个训练周期使用 Product 轨迹重建确定性 80/10/10 数据 manifest，训练结构化 ranker checkpoint，并通过 `FormalTrainingManifestStore` 原子刷新 run manifest hash；下一轮直接读取更新后的 checkpoint。非 `--skip-training` 模式同时强制 Product Executor、真实 feedback、结构化 checkpoint 和 prepared manifest。
- 本轮尚未宣称全量训练开始。E 盘目前只有历史自动化存档和旧 2026-07 feature rows，没有独立新存档的 Product v2 轨迹、正式 policy manifest 或结构化 checkpoint。当时后续计划固定为：在独立新存档上用 `--skip-training` 仅采集最小 Product bootstrap 校准轨迹，离线生成首个真实 checkpoint；随后 prepare/launch 正式闭环并以完整首个游戏日验收。bootstrap 不是正式训练，不能计入启动完成标志。

## 历史记录 · 2026-08-31 当时接续点：EVD-325 Product Executor 之后

- 当时机器可读状态为 `228 registered / 230 semantic / 227 compiler-bound / 145 harness dispatch / 145 Product Executor / 151 five-gate / 62 training allowlist / 2 catalogued blocked`；full snapshot 为 `171 required / 154 readable with provenance / 17 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking 0。原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 0。
- EVD-325 已建立独立 `StardewAI.ProductExecutor`。正式执行地址为 loopback `/api/v1/product/execute`，只接受能力目录支持的非 debug 动作、精确 run-id、执行模式/actor、GUID nonce、精确隔离存档根和有效时间戳；`LiveTrainingLoop` 在非 `--skip-training` 模式下同时强制 Product Executor 与真实 executor feedback，拒绝 RuntimeTestHarness 或无反馈绕过。
- Product Executor 不复制 145 个动作状态机，只在现有唯一原生执行端点前增加产品授权、串行分发、fresh 前后快照、持久化 pending/final 收据和结果契约校验。请求中的决策快照哈希与实际分发前全量哈希均保留；自然世界漂移不再误杀动作，动作级 fresh 前置条件由游戏线程中的现有原生状态机验证，发生漂移时结果强制要求重规划。
- 分发采用 at-most-once 恢复语义：pending 必须在原生 POST 前原子落盘；相同 nonce/相同请求的 final 可跨请求过期窗口幂等返回；nonce 复用不同载荷立即拒绝；重启后发现孤立 pending 时禁止再次下发，落盘为 `native_dispatch_indeterminate_no_replay` 并要求重规划。
- 最终隐藏静音产品冒烟 `3/3` PASS：`artifacts/product-executor-smoke/product-executor-20260831-235239/summary.json`。真实 `executor.tailor_item` 经产品入口 `applied/verified`，同一持久收据重放未二次执行，debug fixture 被产品授权层拒绝。服务级产品测试 `10/10`，全量回归 Core `2191/2191`、Backend `162/162`，Release 解决方案构建 `0 warnings / 0 errors`。
- 当时后续计划不再补动作注册：先用独立新存档重建正式 `policy_decision_trajectory.v1` 数据清单和结构化策略 checkpoint，完成 Product Executor、run-id、快照、manifest、断点恢复和静音进程编排的启动前探针；全部通过后在服务器启动全量训练。启动成功仍以至少一个真实游戏日的连续准入动作、产品原生 verified 回执、数据集/checkpoint 哈希落盘和恢复探针通过为准，不能以进程存在或 `--skip-training` 冒充。

## 历史记录 · 2026-08-31 训练优先动作闭合快照：EVD-324

- 当前冻结状态为 `228 registered / 230 semantic / 227 compiler-bound / 145 harness dispatch / 151 five-gate / 62 training allowlist / 2 catalogued blocked / 0 Product Executor`；full snapshot 为 `171 required / 154 readable with provenance / 17 contextual / 0 blocking`。EVD-317 至 EVD-324 已闭合，顶部旧检查点不得再被解释为当前当时后续计划。
- `story.advance_event -> advance_story_event -> executor.advance_story_event` 已闭合普通事件链。透明桥发布活动事件全部命令、当前命令、问题键、完整响应数组、原生菜单和下一边界；模型只在新问题边界选择响应，编译器重绑事件实例与投影指纹，运行时仅使用原生 `Event.Update` 和菜单输入并在结束、新问题、小游戏或玩家控制序列边界收口。EVD-322 隐藏静音测试的自动消息和第二响应两例均为 `applied/verified`，禁止 `skipEvent`、直接命令索引或已看事件写入。
- `story.advance_event_minigame -> advance_story_event_minigame -> executor.advance_story_event_minigame` 已闭合。透明桥按实时原生类型发布九类剧情小游戏及其运行状态：五种事件过场和 `BoatJourney` 可执行；`GrandpaStory`/`Intro` 明确属于开档与角色创建的玩家边界；1.6.15 无实例化和完成路径的 `TelescopeScene` 保留废弃兼容占位。执行器不调用 `tick`、`forceQuit` 或 `skipEvent`，仅等待 `Game1.currentMinigame` 原生更新，并在 `FantasyBoardGame` 中转发编译器绑定的原生对话输入。EVD-323 隐藏静音测试的被动消息和第二响应均为 `applied/verified`。
- `social.watch_movie -> watch_movie -> executor.watch_movie` 已闭合。模型只选择当周电影、可选宾客和可选零食；编译器以 fresh 快照滚动展开买票、跨图移动、原生邀请、入场、等待宾客、柜台购买和完整放映。EVD-321 隐藏静音矩阵四阶段全部 `applied/verified`，玩家与规范宾客周标记、精确好感增量和影院大厅清理一致。
- 影院运行验证修复了三个不得回退的真实缺陷：进入影院后不得再要求第三张票；柜台/影院门相邻站位必须由透明桥发布当前玩家 BFS 可达性并被核心和运行时共同重绑；同名影院观众实例不得覆盖 `Game1.getCharacterFromName`/`MovieInvitation.invitedNPC` 的规范 NPC 周标记。
- `tailoring.sew_item -> tailor_item -> executor.tailor_item` 已闭合训练优先动作。透明桥实时读取全部 `Data/TailoringRecipes`、库存输入对、原生 Tailoring/`(BC)247` 入口、确定输出或完整随机域、鞋靴属性转移、裁缝历史与余量容量；染色/棱彩外观分支仍归 `tailoring.dye_item` 玩家指令。运行层只使用原生 `TailoringMenu` 输入和 1500ms 生命周期，并回收全部左右余料，不直接改库存、鞋靴、服装、历史或 RNG。
- RuntimeTestHarness 原生闭环不能冒充正式训练执行器。该历史检查点之后的 Product Executor 装配、授权、快照审计、回执持久化和失败重规划已由 EVD-325 完成；不得再把本条解释为当前待办。
- EVD-324 原生烟测为 `3/3` PASS，证据位于 `artifacts/runtime-tailoring/runtime-tailoring-20260831-225159/summary.json`；确定配方、原生随机配方和鞋靴属性转移均为 `applied/verified`。动作对账为 585/585 导出、blocking 0；最终静态回归为 Core `2191/2191`、Backend `155/155`，全解决方案 Release 构建 `0 warnings / 0 errors`。
- 服务器全量训练的启动退出条件是：独立新存档、SMAPI、静音/隐藏、run-id 与快照绑定、非空正式 allowlist、结构化策略检查点、Product Executor 健康、真实执行回执、轨迹 manifest/checkpoint 持久化和可恢复停机全部通过；禁止使用玩家存档、测试 fixture、`--skip-training` 或 baseline 聚合器冒充全量训练。
- 服务器训练成功启动的完成标志不是“进程存在”，而是至少一个真实游戏日产生连续的 `policy_decision_trajectory.v1`，其中所有被选动作均准入、经 Product Executor 原生执行并得到 fresh verified 回执，数据集与检查点哈希落盘，断点恢复探针通过。启动完成后再补两项后置玩家能力。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-316

- `quest.cancel` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、原生 `QuestLog` 输入和严格回执。唯一动作链为 `quest.cancel -> cancel_quest -> executor.cancel_quest`；严格 `PlayerCommandOnly`，不进入默认候选、自主日计划或策略训练。
- 锁定 1.6.15 原生条件为普通 `Quest` 尚未完成、未隐藏、已接受、未等待删除且 `CanBeCancelled()`。透明桥发布当前 `questLog` 中每一条普通任务及阻断诊断、完整身份指纹、任务数和 `acceptedDailyQuest` 副作用。`SpecialOrder.CanBeCancelled()` 恒为 false，且原生点击分支只处理普通 `Quest`，因此特别订单不会被该动作冒领。
- 原版取消按钮没有二次确认菜单：选中任务后点击 `cancelQuestButton` 会立即执行 `accepted=false`、从 `questLog` 删除，并在同日接受的每日委托上清除 `acceptedDailyQuest`。StardewAI 在原生点击前额外要求精确指纹、非空原因和显式玩家确认；fresh 编译器重绑全部任务字段、计数和每日标志。生产执行器只创建原生 `QuestLog`、点击精确任务行和取消按钮，不直接写任务、列表或每日标志。
- 隐藏静音 E 盘矩阵 `4/4` PASS：`artifacts/runtime-quest-cancellation/runtime-quest-cancellation-20260831-011556/summary.json`。覆盖同日每日委托清标志、普通任务保留标志、伪造指纹拒绝和不可取消任务双层拒绝；金钱、任务完成统计和特别订单数量保持不变。
- 最新 full snapshot 为 `163 required / 146 readable with provenance / 17 contextual / 0 blocking`；对账为 `212 registered / 222 semantic / 211 compiler-bound / 137 harness dispatch / 135 five-gate / 55 training allowlist / 10 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2133/2133`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置，下一实际纵向切片为 `rewards.claim_adventure_guild_reward`。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-315

- `processing.crack_geode` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生铁匠对话与 `GeodeMenu` 输入，以及严格领取回执。唯一动作链为 `processing.crack_geode -> crack_geode -> executor.crack_geode`，没有第二套库存、奖励或晶球 RNG 系统。
- 锁定 1.6.15 范围覆盖全部八种基础输入：`(O)275/535/536/537/749/791/MysteryBox/GoldenMysteryBox`。透明预测复刻 `Utility.getTreasureFromGeode` 的计数时序、种子、两段固定次数随机预热、数据驱动掉落、文物保护、Mystery Book 邮件和首次金椰子互斥分支；两个原生共享 `Game1.random` 作物分支只发布完整当季候选族，不伪造确定结果。
- 上游在 Clint 不在铁匠铺、成品工具领取拦截、菜单/事件/控制锁、金钱不足、库存容量不足或端点不可达时排除候选。fresh 编译器重新绑定输入槽位/品质/堆叠、计数、金椰子与金核桃状态、邮件/技能/季节/矿层上下文、输出族、柜台和站位。生产执行器只经 `Blacksmith checkAction -> Process -> GeodeMenu inventory/geodeSpot click -> 2700ms native animation`；不直接写金钱、库存、统计、邮件或团队状态。
- 隐藏静音 E 盘矩阵 `9/9` PASS：`artifacts/runtime-geode-processing/runtime-geode-processing-20260831-003313/summary.json`。八种基础晶球均 `applied/verified`，另含伪造计数拒绝；运行中发现并修正了随机预热循环、产物占原槽后的剩余堆叠归还、首次矿物 `artifactFound` 和金椰子产物转团队金核桃而不留库存四个真实缺口。
- 最新 full snapshot 为 `162 required / 145 readable with provenance / 17 contextual / 0 blocking`；对账为 `210 registered / 221 semantic / 209 compiler-bound / 136 harness dispatch / 133 five-gate / 55 training allowlist / 11 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2128/2128`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定边界继续后置，下一实际纵向切片为 `quest.cancel`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-314

- `player.customize` 已闭合完整透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求和两条原生执行分支。唯一动作链为 `player.customize -> customize_player -> executor.customize_player`，严格 `PlayerCommandOnly`，不进入默认候选、自主日计划或策略训练。
- 巫师神龛分支绑定 `WizardHouseBasement` 的 `WizardShrine`、500g 前态和完整原生控件域：姓名、最爱之物、性别、皮肤 `0..23`、实时 `Farmer.GetAllHairstyleIndices`、饰品 `-1..29`、眼睛与头发六个 HSV 滑条 `0..100`。运行层只经共享移动、`checkAction`、Yes 对话和 `CharacterCustomization(Source.Wizard)` 控件/OK 输入，不直接写金钱或外观。
- 沙漠改造分支明确读取替换地图 `DesertFestival`，不是普通 `Desert`；绑定当前造型师、每日标志、装备回收所需空位、完整 `Data/MakeoverOutfits` 过滤和原生日存档 RNG 投影。运行层走上 `DesertMakeover` TouchAction、跳过原生事件并等待 `ReceiveMakeOver` 完成回调，不直接写装备、每日标志或调用回调。
- 源边界保持分离：染色仍属 `tailoring.dye_item`；NewGame/HostNewFarm/NewFarmhand 属于建档控制面；锁定基础版调用点中没有可达 Dresser 构造链。隐藏静音 E 盘矩阵 `4/4` PASS：`artifacts/runtime-player-customization/runtime-player-customization-20260830-215722/summary.json`，含巫师全字段、六滑条 100 边界、伪造金钱拒绝与沙漠改造。
- 最新 full snapshot 为 `161 required / 144 readable with provenance / 17 contextual / 0 blocking`；对账为 `208 registered / 220 semantic / 207 compiler-bound / 135 harness dispatch / 131 five-gate / 54 training allowlist / 12 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2123/2123`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定边界后置，下一实际纵向切片为 `processing.crack_geode`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-313

- `player.choose_jukebox_track` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生 Saloon 点唱机菜单输入和严格回执。唯一动作链为 `player.choose_jukebox_track -> choose_jukebox_track -> executor.choose_jukebox_track`，没有第二套音乐切换或 Mini-Jukebox 状态写入系统。
- 锁定 1.6.15 规则为 Saloon `Buildings[(1,17),(2,17)] Action=Jukebox` 打开 `ChooseFromListMenu`；曲目按 `Utility.GetJukeboxTracks` 的原生顺序生成：先收录 `Data/JukeboxTracks` 中 `Available=true` 的行，再规范化 `Farmer.songsHeard` 的替代曲目 ID，仅保留 soundbank 中实际存在且未被数据明确禁用的曲目。菜单从索引 0 开始，前进按钮循环，OK 调用原生选择动作但菜单不自动关闭，Cancel 才关闭。绿雨期间非 `rain` 曲目由原生 `Game1.changeMusicTrack` 规则禁止。
- 该能力严格为 `PlayerCommandOnly`：玩家必须给出精确曲目、原因和确认；未知、未解锁、绿雨期不合法或指纹漂移的曲目在上游排除，不进入默认候选、自主日计划或策略训练。fresh 编译器从当前曲目目录、Saloon 端点、站位、天气和默认音乐轨重绑定全部机械字段。生产执行器只使用共享移动、原生 `checkAction` 以及菜单的前进/OK/Cancel `receiveLeftClick`，不直接调用 `Game1.changeMusicTrack`，不写播放、解锁或 Mini-Jukebox 状态。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-jukebox-selection/runtime-jukebox-selection-20260830-201036/summary.json`。原生第一首、循环前进到最后一首均 `applied/verified`，伪造曲目索引被严格拒绝；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新 full snapshot 为 `160 required / 143 readable with provenance / 17 contextual / 0 blocking`；对账为 `206 registered / 219 semantic / 205 compiler-bound / 134 harness dispatch / 129 five-gate / 54 training allowlist / 13 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2118/2118`、Backend `153/153`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.customize`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-312

- `player.choose_bobber` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生鱼店菜单输入和严格回执。唯一动作链为 `player.choose_bobber -> choose_bobber_style -> executor.choose_bobber_style`，没有第二套钓鱼装备或偏好写入系统。
- 锁定 1.6.15 规则为 FishShop `Buildings[10,4] Action=Bobbers` 打开 `ChooseFromIconsMenu("bobbers")`；固定样式 `0..38` 的解锁条件是 `style_id <= fishCaught.Count()/2`，随机样式为 `-2` 且始终可选。声呐浮标 `(O)789` 只把实际显示样式临时覆盖为 `39`，不改变本动作可选的偏好目录。随机样式只在原生 `FishingRod.getBobberStyle` 使用阶段消耗共享 RNG；透明读取不推进 RNG。
- 该能力严格为 `PlayerCommandOnly`：必须给出精确样式、原因和确认；锁定样式在上游排除，不进入默认候选、自主日计划或策略训练。fresh 编译器从当前鱼获数、偏好、FishShop 端点和站位重绑定全部机械字段。生产执行器只调用原生 `checkAction`、准确非锁定图标的 `receiveLeftClick` 和原生关闭按钮，不直接写 `bobberStyle`、`usingRandomizedBobber` 或 RNG。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-bobber-selection/runtime-bobber-selection-20260830-193216/summary.json`。固定样式 7、随机样式 -2 均 `applied/verified`，鱼获种类数为 2 时固定样式 2 被严格拒绝；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新 full snapshot 为 `159 required / 142 readable with provenance / 17 contextual / 0 blocking`；对账为 `204 registered / 218 semantic / 203 compiler-bound / 127 five-gate / 54 training allowlist / 14 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2112/2112`、Backend `152/152`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.choose_jukebox_track`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-311

- `multiplayer.send_chat` 已闭合完整透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、原生 ChatBox 输入和严格发送者本地回执。唯一动作链为 `multiplayer.send_chat -> send_multiplayer_chat -> executor.send_multiplayer_chat`，没有第二套聊天网络协议。
- 该能力严格为 `PlayerCommandOnly`：玩家必须给出作用域、原因、正文和确认；只接受普通全局消息或绑定当前在线玩家的私聊。任意 `/` 命令、控制字符、模糊私聊目标、超出原生 880px 内容宽度和未确认文本均在上游排除，不进入默认候选、自主规划或策略训练。
- fresh 编译器从 `player.multiplayer_chat` 重绑发送者、语言、默认颜色、网络角色、消息数、输入宽度、在线收件人原生顺序与精确 first-match。运行层只经 `ChatBox.activate -> ChatTextBox.RecieveTextInput -> ChatBox.textBoxEnter`，由原版完成脏词过滤、全局 `AllPlayers` 或私聊 exact recipient 的 type-10 传输；不直接调用 `sendChatMessage`/`receiveChatMessage`，也不伪造远端送达回执。
- 隐藏静音 E 盘矩阵 `2/2` PASS：`artifacts/runtime-multiplayer-chat/runtime-multiplayer-chat-20260830-183025/summary.json`。全局 kind 0 与私聊 kind 3 均 `applied/verified`，输入框原生复位、消息上限、语言、颜色、格式化片段和发送者本地回执全部一致。
- 最新 full snapshot 为 `158 required / 141 readable with provenance / 17 contextual / 0 blocking`；对账为 `202 registered / 217 semantic / 201 compiler-bound / 125 five-gate / 54 training allowlist / 15 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2105/2105`、Backend `151/151`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.choose_bobber`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-310

- `multiplayer.manage_wallet` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 ManorHouse LedgerBook 菜单输入和严格回执。唯一动作链为 `multiplayer.manage_wallet -> manage_multiplayer_wallet -> executor.manage_multiplayer_wallet`，没有第二套钱包执行系统。
- 完整命令域为 `schedule_separate`、`cancel_separate`、`schedule_merge`、`cancel_merge`、`transfer`。前四项只允许房主；转账允许独立钱包模式下的实际玩家，收款人严格绑定原生 `Game1.getAllFarmers()` 已认领顺序、响应键和玩家 ID。模式更改只设置今晚待结算状态，次日由原生 `ManorHouse.SeparateWallets/MergeWallets` 完成；独立钱包按原生整数除法分配共享余额并舍弃余数，合并钱包汇总已认领玩家余额。转账通过原生收款人对话和 `DigitEntryMenu` 结算。
- 该能力严格为 `PlayerCommandOnly`：要求原因和显式确认，转账另需独立转账确认；默认候选、自主规划和策略训练均排除。fresh 编译器只保留操作意图，并从当前透明投影重绑模式、参与者、余额、收款人响应键、LedgerBook 站位和投影指纹。
- 隐藏静音 E 盘矩阵 `7/7` PASS：`artifacts/runtime-multiplayer-wallet/runtime-multiplayer-wallet-20260830-154907/summary.json`。5 个即时命令和共享转独立、独立转共享两个原生次日结算全部 `applied/verified`。生产执行器不直接写钱包模式、待结算标记、共享余额、个人余额或赠款统计。
- 最新 full snapshot 为 `157 required / 140 readable with provenance / 17 contextual / 0 blocking`；对账为 `200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 training allowlist / 16 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2096/2096`、Backend `150/150`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定决定继续复用现有等价执行并后置完美代打，下一实际纵向切片为 `multiplayer.send_chat`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-309

- `mining.activate_calico_statue` 已闭合透明读取、自主候选、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 MineShaft 地块交互和严格结果回执。唯一动作链为 `mining.activate_calico_statue -> activate_calico_statue -> executor.activate_calico_statue`；小模型只接受或拒绝当前精确投影的下一效果，移动、交互和结果验收由机械层完成。
- 锁定 1.6.15 规则使用 `Utility.CreateDaySaveRandom(totalActivatedToday + 1)`、队伍平均每日运气和当前效果栈选择下一效果。透明桥发布完整 18 效果目录、当前/预计效果栈、评分、奖励蛋、生命/耐力/速度回执、雕像 `284 -> 285` 地块状态和可达相邻站位；非沙漠节骷髅洞、非房主、已激活、地块漂移或无站位均在上游排除。
- 隐藏静音 E 盘矩阵 `18/18` PASS：`artifacts/runtime-calico-statue/runtime-calico-statue-20260830-144828/summary.json`。全部效果 ID `0..17` 均经原生 `MineShaft.checkAction` 应用并验证；四档蛋奖励 `10/25/50/100`、速度、完全恢复、评分 `+1`、效果字典和单次地块转换均一致。生产执行器不直接写评分、效果、奖励、生命、耐力、Buff、地块或 RNG。
- 最新 full snapshot 为 `156 required / 139 readable with provenance / 17 contextual / 0 blocking`；对账为 `198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 training allowlist / 17 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2087/2087`、Backend `149/149`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `multiplayer.manage_wallet`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-308

- `minigame.play_slots` 已闭合透明读取、Rarecrow 齐币需求候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生 `ClubSlots`/下注按钮输入和随机结算回执。唯一动作链为 `minigame.play_slots -> play_slots -> executor.play_slots`；小模型只决定是否安排一次下注，转轴、RNG、倍率、齐币和统计完全由原版游戏产生。
- 锁定 1.6.15 规则为 10/100 两档下注；`DailyLuck` 与 `LuckLevel` 形成 `1 + DailyLuck*2 + LuckLevel*0.08` 倍概率阈值。透明桥发布完整 11 分支概率/图案/倍率、期望收益、四台实时机器、活动转轴及赌场商店。共享 `Game1.random` 不具备稳定独占前瞻，因此明确采用 live feedback，不伪造确定结果。
- 自动候选严格限定为缺少 `(BC)126`、Deluxe Scarecrow 依赖开放且齐币不足 10000 时，每次只旋转一次并结算后重规划。`BuyQiCoins` 继续由既有 `executor.choose_dialogue_response` 原生分支负责，不复制购币执行器。
- 隐藏静音 E 盘矩阵 `2/2` PASS：`artifacts/runtime-slots-smoke/runtime-slots-smoke-20260830-132407/summary.json`。10 币无奖分支 `50->40`，100 币单七 `x2` 分支 `1000->1100`；两次均原生 `timesPlayedSlots +1`、图案/倍率/币差一致且 Done 后退出。
- 最新 full snapshot 为 `155 required / 139 readable with provenance / 16 contextual / 0 blocking`；对账为 `196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 training allowlist / 18 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2080/2080`、Backend `148/148`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `mining.activate_calico_statue`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-307

- `minigame.play_prairie_king` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线和定时等价执行。唯一动作链为 `minigame.play_prairie_king -> play_prairie_king -> executor.play_prairie_king`；小模型只学习是否、何时安排，机械层始终执行不可见等价会话。
- 该边界适用于训练和正式 AI 陪玩：AI 不进行原生逐帧草原大王操作。运行层经原生 `Arcade_Prairie` 创建 `AbigailGame`，在 108000 游戏 tick 等价预算期间暂停开始界面，随后只调用原生 `usePowerup(-3)`，由游戏自身增加通关/无伤统计、补发 `Beat_PK`、检查成就并发送全局消息。结果必须记录为 `simulated_equivalent` 和 `not_native_perfect_proxy_play`。
- 隐藏静音 E 盘冒烟 `runtime-prairie-king-smoke-20260830-120617` PASS：原生统计 `0->1`、无伤统计 `0->1`、`Beat_PK=True`，最终闸门为 `complete_prairie_king_without_dying`。执行器未直接写统计、邮件、成就或奖励。
- 最新 full snapshot 为 `154 required / 138 readable with provenance / 16 contextual / 0 blocking`；对账为 `194 registered / 213 semantic / 193 compiler-bound / 117 five-gate / 52 training allowlist / 19 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2075/2075`、Backend `148/148`、Release `0 warnings / 0 errors`。
- Junimo Kart 的 AI actor 继续复用已有 `timed_equivalent`，覆盖训练、陪玩和专用房主；其原生完美代打只保留为核心能力训练完成后的 `PlayerCommandOnly` 占位，不阻塞主线。下一实际纵向切片为 `minigame.play_slots`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-306

- `minigame.play_darts` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生交互/确认/鼠标输入和限量核桃回执。唯一动作链为 `minigame.play_darts -> play_darts -> executor.play_darts`；小模型只决定是否完成下一轮，瞄准、充能、投掷和结果对话均由机械执行层负责。
- 锁定 1.6.15 规则为非雨、偶数日、20:00 后的海盗夜；`Darts` 限量掉落计数 `0/1/2` 分别给 `20/15/10` 支飞镖，最多发放 3 个核桃。六投精确方案为 `T20,T20,T20,T20,T17,D5`，总分 301。候选在跨地图时直接读取 `IslandSouthEastCave.IsRainingHere()`，不会误用玩家当时地图天气。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-darts-game-smoke/runtime-darts-game-smoke-20260830-110428/summary.json`。三轮均由原生 `DartsGame`、Yes 对话和鼠标按下/释放完成，命中 `60,60,60,60,51,10`，6 投清零并依次得到第 1、2、3 个核桃；生产路径不写分数、投掷数、计时器、RNG、奖励或进度。
- 最新 full snapshot 为 `153 required / 137 readable with provenance / 16 contextual / 0 blocking`；对账为 `192 registered / 212 semantic / 191 compiler-bound / 115 five-gate / 51 training allowlist / 20 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`，冻结状态为 `frozen`，KnowledgeCompiler `585/585`、blocking 0；Core `2071/2071`、Backend `148/148`、Release `0 warnings / 0 errors`。下一冻结目录切片为 `minigame.play_junimo_kart`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-305

- `minigame.play_crane_game` 已闭合透明读取、玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生机器/确认对话/小游戏方向输入和原生奖励菜单回执。唯一动作链为 `minigame.play_crane_game -> play_crane_game -> executor.play_crane_game`；该动作严格为 `PlayerCommandOnly`，要求显式确认，不进入默认候选或策略训练。
- 锁定 1.6.15 反编译确认一次会话原生收费 500g、3 次机会、每次 900 tick，只接受先向右再向下的方向输入。机器占用规则、当日电影奖池覆盖、基础奖池、实时奖品/传送带/爪钩/掉落检查/计时与生命均由 `player.crane_game` 发布；奖品使用共享 `Game1.random`，因此不伪造确定性预测，执行器每次只按实时状态选择可达奖品。
- 隐藏静音 E 盘冒烟 `1/1` PASS：`artifacts/runtime-crane-game-smoke/runtime-crane-game-smoke-20260830-095512/summary.json`。原生扣费 `10000->9500`，完成 3 次机会，2 件奖品经原生 `ItemGrabMenu` 转入背包，小游戏和菜单正常退出。方向输入固定在 `UpdateTicking`，生产路径不写 Money、RNG、奖品/爪钩位置、小游戏状态或背包结果。
- 最新 full snapshot 为 `152 required / 136 readable with provenance / 16 contextual / 0 blocking`；对账为 `190 registered / 211 semantic / 189 compiler-bound / 113 five-gate / 50 training allowlist / 21 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结、blocking 为 0；Core `2066/2066`、Backend `148/148`、Release `0 warnings / 0 errors`。下一冻结目录切片为 `minigame.play_darts`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-304

- `minigame.play_calico_jack` 已闭合透明读取、自主需求候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生牌桌/对话/小游戏输入和严格齐币结算回执。唯一动作链为 `minigame.play_calico_jack -> play_calico_jack -> executor.play_calico_jack`；生产执行不直接写牌、RNG、齐币、牌局结果或统计。
- 锁定 1.6.15 反编译确认低注 `ClubCards` 为 100、高注 `BlackJack 1000`，初始牌和后续抽牌共用由 `timesPlayedCalicoJack / DaysPlayed / uniqueID` 建立的确定性随机流；庄家 18/19/20 的对玩家分支、抽牌偏置和结算均已完整复刻到只读投影。Wiki 关于 Luck 不影响牌局的概述不能覆盖源码：基础牌局不消费 Luck，但原生 Qi fruit `999` 分支明确读取 `DailyLuck + LuckLevel`，透明桥保留这一冲突及实时值。
- 自主候选严格限定为玩家缺少赌场 Rarecrow `(BC)126` 且 Deluxe Scarecrow 依赖仍开放时补足 `10000` 齐币。每次只允许一局；正收益种子且余额至少 1000 时选高注，损失/平局种子降到低注，若只剩 100 且投影为损失则上游延后。该动作是 `R2 Consumptive / PolicyAuthorizationRequired`，不是逐局显式确认，因此在证据闭合后进入策略训练 allowlist。
- 隐藏静音 E 盘矩阵 `3/3` PASS：高注 seed 1 站牌庄家爆牌 `+1000`、低注 seed 23 站牌庄家更高 `-100`、低注 seed 4 首次要牌后庄家爆牌 `+100`。证据为 `artifacts/runtime-calico-jack-smoke/runtime-calico-jack-smoke-20260830-085329/summary.json`。
- 最新 full snapshot 为 `151 required / 135 readable with provenance / 16 contextual / 0 blocking`；对账为 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 training allowlist / 22 catalogued blocked / 0 Product Executor`。原生分母为 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 0；Core `2060/2060`、Backend `148/148`、Release `0 warnings / 0 errors`。冻结目录的下一切片为 `minigame.play_crane_game`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-303

- `island.field_office_survey` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 `FieldOfficeSurvey -> Survey_Yes -> PurpleFlowerSurvey/PurpleStarfishSurvey` 对话输入和严格结算回执。唯一动作链为 `island.field_office_survey -> answer_field_office_survey -> executor.answer_field_office_survey`；生产执行不直接写植物、当日失败锁、核桃标记、debris、邮件或 finale。
- 锁定 1.6.15 反编译确认紫花答案 `22`、紫海星答案 `18`、答错后当天禁答、`IslandFieldOffice.DayUpdate` 次日清锁、两侧各一次 collected-nut、`GoldenWalnutsFound < 130` 时先生成 `(O)73` debris、磁力拾取后核桃数 `+1`，以及 11 件化石和两项调查全完成后的 finale。已有核桃 debris 会在上游要求先拾取，避免路线移动造成投影漂移。
- 隐藏静音 E 盘最终矩阵 `9/9` PASS：两道普通答案、同日连续两题、错误答案日锁、原生 DayUpdate 解锁、两道 `GoldenWalnutsFound=130` 无掉落分支和最后一题触发 finale 全部匹配。运行层同时捕获瞬时原生 debris 生成与共享磁力拾取后的最终 debris/核桃计数。证据为 `artifacts/runtime-field-office-survey-smoke/runtime-field-office-survey-final-20260830/summary.json`。
- 最新 full snapshot 为 `148 required / 132 readable with provenance / 16 contextual / 0 blocking`；对账为 `186 registered / 209 semantic / 185 compiler-bound / 109 five-gate / 49 training allowlist / 23 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；回归为 Core `2054/2054`、Backend `148/148`、Release `0 warnings / 0 errors`。下一目录切片为 `minigame.play_calico_jack`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-302

- `island.field_office_donate` 已闭合透明读取、显式确认候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 `FieldOfficeDesk -> Safari_Donate -> FieldOfficeMenu` 输入和严格结算回执。唯一动作链为 `island.field_office_donate -> donate_field_office_piece -> executor.donate_field_office_piece`；生产执行不直接写 fossil、奖励、核桃标记、邮件或 finale。
- 锁定 1.6.15 反编译确认 11 个槽位和物品映射，其中 `(O)823` 按槽 0 后槽 2、`(O)826` 按槽 7 后槽 6 的原生顺序解析。透明桥同时公开解锁/教授/互斥锁/菜单、Desk 与 Survey 端点、11 项捐赠状态、四组恢复状态、两道调查状态与答案、finale readiness、GoldenWalnutsFound、未领取奖励和每个可执行捐赠候选。
- 隐藏静音 E 盘最终矩阵 `15/15` PASS：11 个槽位逐一原生捐赠，中心骨架奖励 `(O)73 x6 + (O)69`、蛇骨架 `(O)73 x3 + (O)835`，蝙蝠/青蛙普通核桃奖励，以及 `GoldenWalnutsFound=130` 时 `(O)TentKit` / `(O)926` 替代奖励全部匹配。最终证据为 `artifacts/runtime-field-office-donation-smoke/runtime-field-office-donation-smoke-20260830-063003/summary.json`。
- 全仓库分母复核纠正了此前将 `FieldOfficeSurvey` 错并入捐赠的问题。它现作为独立 `island.field_office_survey` 保持 `catalogued_blocked`；22/18 两个固定答案和调查状态已透明，但候选、编译和原生执行尚未借用捐赠证据。最新 full snapshot 为 `148 required / 132 readable with provenance / 16 contextual / 0 blocking`；对账为 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 training allowlist / 24 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；回归为 Core `2049/2049`、Backend `148/148`、Release `0 warnings / 0 errors`。下一切片固定为 `island.field_office_survey`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-301

- `housing.renovate` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、共享碰撞网格站位、Robin 原生对话、`HouseRenovations` 商店和 `RenovateMenu` 世界区域点击。唯一动作链为 `housing.renovate -> executor.renovate_home`；住宅升级、建筑建造/外观、家具摆放和移动继续使用各自既有实现。
- 透明桥实时读取完整 `Data/HomeRenovations`，锁定基础 1.6.15 的 18 个条目、原生可用顺序、`Value`/`Mail` 要求与动作、全部矩形组、婴儿床特殊区域、阻挡、价格、`FirstPurchase_<RoomId>` 和退款资格。数据 payload SHA-256 为 `26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b`；目录数量、顺序、字段或哈希漂移均失败关闭。
- 全部 18 项严格保持 `PlayerCommandOnly`：必须给出精确 `renovation_id`、`selected_index`、原因和确认；破坏动画另需破坏性确认。它们从默认候选、策略训练和 allowlist 排除。LiveTrainingLoop 已补齐 `PlayerCommand` 调用来源传输；编译器从 fresh `locations.collision_grid` 派生或复核可达柜台站位，不再要求模型提供机械站位。
- 隐藏静音 E 盘矩阵 `19/19` PASS：18 个实时原版条目全部经原生 Robin/商店/装修菜单执行，并额外覆盖负价项目在没有首次购买标记时不退款。跨地图续跑会锁定同一装修 ID/区域，保留玩家指令来源与两级确认，到达服务地图后才重建终端动作。full snapshot 为 `146 required / 130 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 training allowlist / 24 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；完整回归为 Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`；下一语义切片固定为 `island.field_office_donate`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-300

- `foraging.rummage_garbage` 已闭合透明读取、上游候选排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生垃圾桶交互和输出/任务反馈。唯一动作链为 `rummage_garbage -> executor.rummage_garbage`；移动、物品接收、debris 拾取和普通/特别收集任务继续复用既有实现。
- 锁定 1.6.15 `performAction Garbage -> CheckGarbage -> TryGetGarbageItem` 与实时 `Data/GarbageCans`。透明桥在一次 Buildings 扫描中发布 Garbage action、桶 ID、当日已查集合、`trashCansChecked`、DailyLuck、Alleyway Buffet、确定性无副作用预测、选中数据行、精确物品状态/品质/数量/交付方式、目击 NPC 及反应、空工具栏槽、数据哈希与投影指纹。原版日随机只在预测副本上计算，不推进游戏 RNG。
- 当日已查、未知/漂移数据、无安全槽、不可达及负友谊目击在上游直接排除；无物品是有效原生结果，Linus 的精确正友谊分支允许执行。运行层只调用一次 `GameLocation.checkAction`，核对 CheckedGarbage、统计 `+1`、背包加 debris 的精确单位状态守恒、可选 Linus 好感及槽位恢复，不直接写结果状态。
- 隐藏静音 E 盘矩阵 `9/9` PASS：普通输出、无输出、垃圾帽直接入包、沙漠节多 debris、当日已查排除、负面目击排除、Linus 正向目击、普通收集任务和特别订单。full snapshot 为 `146 required / 130 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 training allowlist / 25 catalogued blocked / 0 Product Executor`。完整回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`；下一语义切片固定为 `housing.renovate`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-299

- `foraging.harvest_tree_product` 已闭合普通原版 `Tree` 的透明读取、上游排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 terrain BFS、原生摇树与完整输出域回执。唯一动作链为 `harvest_tree_product -> executor.harvest_tree_product`；果树、砍树、清障和苔藓采集继续使用各自既有语义，不新增第二套移动或拾取系统。
- 锁定 1.6.15 `Tree.performUseAction -> shake` 与 `Data/WildTrees` 确认：成熟、非树桩、未 tapped、有种子、满足原生采集等级门且 `maxShake==0` 时才形成候选。默认种子、秋季枫树 `(O)408` 榛子替换、岛屿棕榈 `(O)88` 椰子及可选 `(O)791` 金色椰子均按实时地点和数据投影；神秘箱、金色动物饼干、原生稀有物品族、技能书和齐豆只发布完整有界输出域，不预读 RNG。锁定数据的榛子日期为秋 14–28 日；Wiki 页面存在 14/15 日起始描述差异，运行合同以数据与反编译为准。
- 运行层到站后冻结背包与 debris 的联合输出基线，临时切到经 fresh snapshot 验证的空工具栏槽，只调用一次 `GameLocation.checkAction`，并在 `finally` 恢复槽位；不直接调用 `Tree.shake`，不写树、RNG、debris、库存或技能。隐藏静音 E 盘矩阵全部 PASS：普通种子、秋季榛子、岛屿棕榈、无种子排除、摇动中排除、tapped 排除；成功分支验证确定输出、可选输出域、种子消耗、当日摇动标记、零 Foraging XP 与槽位恢复。
- full snapshot schema 仍为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 training allowlist / 26 catalogued blocked / 0 Product Executor`。完整回归为 Core `2035/2035`、Backend `144/144`，全解构建 `0 warnings / 0 errors`；下一语义切片固定为 `foraging.rummage_garbage`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-298

- `foraging.harvest_fruit_tree` 已闭合当前地点果树的透明读取、上游候选排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS 与原生摇树回执；唯一动作链为 `harvest_fruit_tree -> executor.harvest_fruit_tree`，没有新增第二套路由或移动系统。
- 锁定 1.6.15 `FruitTree.performUseAction -> shake` 确认：成熟且非树桩、`maxShake==0`、实时 `fruit` 非空才可收获；普通分支保留每个 live Item 的标识、品质和数量，雷击分支按果槽替换为零品质 `(O)382` 煤炭；原生收获清空果实且不给 Foraging XP。季节只影响后续产出，不阻挡树上已有果实的摇取。官方 Wiki 仅二次确认每日一果、最多积累三果、树龄品质与雷击煤炭。
- 运行层只调用一次 `GameLocation.checkAction`，不直接调用 `FruitTree.shake`，也不写果实、debris、库存或技能。隐藏静音 E 盘矩阵全部 PASS：单果普通、三果金星、三份雷击煤炭、空树上游排除、摇动中上游排除；每个成功分支均核对完整 `qualified_item_id + quality + quantity` 守恒和零经验变化。
- full snapshot schema 仍为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`，因为新增内容位于既有 `current_location.terrain_features` 行内；权威对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 training allowlist / 27 catalogued blocked / 0 Product Executor`。完整回归为 Core `2031/2031`、Backend `143/143`；下一语义切片固定为 `foraging.harvest_tree_product`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-297

- `fishing.manage_fish_pond` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求和原生 `PondQueryMenu` 回执；复用既有鱼塘 BFS/站位基础设施，不替代 `fishing.service_fish_ponds` 的产出收取与请求交付链。
- 该动作只接受玩家明确给出的鱼塘坐标、`cycle_netting|empty_pond` 和原因；清塘还需操作级 `confirm_empty_pond=true`。它是 `PlayerCommandOnly`，不进入默认候选、策略训练或 allowlist，避免模型因装饰偏好或破坏性重置自主操作鱼塘。
- 锁定 1.6.15 反编译确认：换网只执行 `(nettingStyle+1)%4`；`ClearPond` 按当前鱼数生成同种鱼 debris，并重置鱼种/数量/请求项/天数/闸门/金色饼干/生成标记/水色，同时保留容量、请求完成标记、标牌、产出引用和网样式。官方 Wiki 仅作清塘语义与金色饼干丢失的二次核验。
- 隐藏静音 E 盘隔离运行 `runtime-fish-pond-management-20260830-013602` PASS：换网 `2->3` 且经济状态不变；清塘 `fish_count 1->0`，精确鱼 debris、重置字段、保留字段和工具栏恢复均通过。入口使用一次性作用域右键边沿进入 `GameLocation.checkAction -> FishPond.doAction`，不构造菜单、不调用 `ClearPond`、不直接写鱼塘状态。
- 最新 full snapshot schema 为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 training allowlist / 28 catalogued blocked / 0 Product Executor`。完整回归为 Core `2027/2027`、Backend `142/142`；下一语义切片固定为 `foraging.harvest_fruit_tree`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-296

- `festival.spin_wheel` 已闭合秋季 16 日展览会转盘的透明读取、Stardrop 有界需求候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生随机回执。自动候选仅在未获得 Fair Stardrop、扣除尚未领取的陈列奖励后仍缺至少 `2` 星币时出现；恰好缺 `1` 星币继续交给免费力量小游戏。
- 锁定 1.6.15 反编译确认 Buildings `308/309`、`wheelBet -> Green -> NumberSelectionMenu -> WheelSpinGame`、1000ms 预启动、固定减速度、`LuckLevel` 重试门和原生 `+/- wager` 结算。零幸运 30 个等可能构造结果中绿方胜 `22`、橙方胜 `8`，因此下注使用零幸运 Kelly 比例 `7/15`，再按剩余需求封顶；Wiki 的 73.3% 与 46.7% 只作玩家可见规则二次核验。
- 执行器复用共享 BFS，点击真实 `Green` 响应，在原生数字菜单输入精确下注，然后等待真实转盘、结果文字、菜单退出和人物控制恢复。它不写 RNG、旋转速度、得分、下注结算或菜单结果；原生胜负都属于有效训练反馈。
- 隐藏静音 E 盘隔离运行 `runtime-fair-wheel-spin-20260830-005054` PASS：两次真实下注均为 `466`，分别覆盖 `1000->1466` 的胜局和 `1000->534` 的负局，原生结果与清理均通过。
- 最新 full snapshot schema 为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 training allowlist / 29 catalogued blocked / 0 Product Executor`。完整回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `fishing.manage_fish_pond`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-295

- `festival.play_strength_game` 已闭合秋季 16 日展览会力量小游戏的透明读取、精确一枚星币的 Stardrop 缺口候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生回执。该动作免费但固定只奖励 `1` 星币，因此自动候选仅在扣除尚未领取的陈列奖励后恰好还缺 `1` 星币时出现。
- 透明桥发布 Buildings 图块 `540`、站立 X=`29`、实时 `StrengthGame` 私有状态、力量 `0..100`、变化速度 `3/4`、摆锤方向、点击/结果/透明度/计时器、玩家动画状态、完整 Fair 商店行和共享星币缺口。固定合同来自锁定 1.6.15 反编译，活动状态逐帧读取。
- 执行器复用共享 BFS，到位并等待原生移动输入完全结算后，经 `Event.checkAction -> StrengthGame.receiveLeftClick -> FarmerSprite.animateOnce(168,80ms,8) -> StrengthGame.afterSwingAnimation` 进入和完成原版流程。它预测点击后 `9` 次原生力量更新，只点击一次，不写 power、festivalScore、计时器、菜单或玩家位置。
- 隐藏静音 E 盘隔离运行 `runtime-fair-strength-game-20260829-234510` 与 `runtime-fair-strength-game-20260829-234614` 均 PASS，分别覆盖 changeSpeed `4` 与 `3`：点击力量 `64->100`、`72->99`，星币均为 `1999->2000`，原版结果对话和清理完成。
- 最新 full snapshot schema 为 `143 required / 127 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 training allowlist / 30 catalogued blocked / 0 Product Executor`。完整回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.spin_wheel`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-294

- `festival.play_slingshot_game` 已闭合秋季 16 日展览会靶场的透明读取、Stardrop 有界需求候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生 TargetGame 回执。模型只决定是否花费 50g 开始一轮；自动候选仅在“当前星币 + 尚未领取的展览陈列奖励”仍不足 2000 星币且 Stardrop 未获得时出现。
- 透明桥实时发布 Buildings 501/502、50g 入场费、1000/50000/1000/16100ms 四段时序、79 个锁定目标、实时目标私有状态、临时 `(W)32` 与 `(O)390 x999`、命中/准确率/倍率/奖励公式、全部 Fair 商店行和共享星币缺口。固定规则来自锁定 1.6.15 反编译；活动实例与目标状态均实时核对。
- 执行器复用共享 BFS 和普通矿井弹弓唯一的 `SlingshotAimPatch`，经 `Event.checkAction -> DialogueBox.receiveLeftClick -> TargetGame` 进入原版小游戏，只调用原生按下/蓄力/释放输入。它不写 Money、计时器、目标、score、accuracy、festivalScore 或库存；原版拥有弹药、碰撞、命中、得分、奖励和清理。
- 隐藏静音 E 盘隔离运行 `runtime-fair-slingshot-game-20260829-225533` PASS：48 次原生发射、48 次有效命中，raw score `95`、accuracy `102`、final score `380`，原版高分封顶奖励 `500` 星币；50g 扣款、节庆返回、临时弹弓/弹药清理均通过。
- 最新 full snapshot schema 为 `142 required / 126 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 training allowlist / 31 catalogued blocked / 0 Product Executor`。完整回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_strength_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-293

- `festival.play_fishing_game` 已闭合秋季 16 日展览会钓鱼小游戏的透明读取、上游需求候选、DailyPlan、fresh 编译重绑定、类型化请求和原生执行回执。自动候选只在展览会进行中、玩家有 50g，且“当前星币 + 尚未领取的展览陈列奖励”仍不足以购买未获得的 `(O)434` Stardrop 时出现；其他商店行保持透明，但不会形成无限刷小游戏的自动需求。
- 透明桥实时发布 Buildings 503/504 交互图块、50g 入场费、100000ms 游戏时长、11100ms 结果时长、临时 Bamboo Pole / `(O)690` bait / `(O)687` tackle、原版得分与星币公式、当时小游戏私有计时器、全部星币商店行、Stardrop 价格与投影缺口。字段来自锁定 1.6.15 反编译和实时对象，不按 Wiki 示例推断。
- 执行器复用共享 BFS/连续移动与普通钓鱼的 BobberBar 控制器，只经 `Event.checkAction -> DialogueBox.receiveLeftClick -> Event.answerDialogue -> FishingGame` 进入原版小游戏；输入在 `UpdateTicking` 物理更新前发出，不直接写 Money、score、perfections、starTokensWon 或 festivalScore。随机完美率作为训练反馈，执行可靠性由合法输入、完整时序、精确原版公式、星币到账、节日返回和临时钓具清理共同验证。
- 隐藏静音 E 盘隔离运行 `runtime-fair-fishing-game-20260829-221047` PASS：原版 100 秒会话钓到 `5` 条有效鱼且 `5/5` 完美，最终 `364` 分、`432` 星币；50g、原版三连完美翻倍/奖励公式、返回图块和临时状态全部通过。首轮烟测同时抓到并修正了错误的普通对话调用，最终入口使用真实节日菜单点击。
- 最新 full snapshot schema 为 `141 required / 125 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 training allowlist / 32 catalogued blocked / 0 Product Executor`。完整回归为 Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_slingshot_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-292

- `festival.manage_grange_display` 已闭合秋季 16 日星露谷展览会陈列的透明读取、确定性最优九件选择、上游候选、DailyPlan、fresh 编译重绑定和原生执行回执。高层动作负责“为一等奖准备陈列/评审后取回”，进入策略训练；`executor.manage_grange_display` 每次只执行一件物品的放入或取回，严格为 `ExecutorCalibration`。
- 透明桥实时读取共享 `FarmerTeam.grangeDisplay`、玩家库存、原生实际售价 `sellToStorePrice(-1L)`、品质、八类多样性、九件数量分、Mayor 短裤惩罚、评审状态、展台交互图块和 `grangeMutex`。优化器按原版公式计算当前分与可达最优分，不依赖文档示例或静态物价。
- 生产执行器复用共享 BFS/移动状态机，到达相邻格后只调用节日 `Event.checkAction` 打开原生 `StorageContainer`，通过菜单点击完成一次变更并等待共享互斥锁释放；禁止直接写展台、库存、评分或评审状态，异常列表形状也只会 fail-closed。动作不会替玩家启动评审。
- 隐藏静音 E 盘隔离运行 `runtime-grange-display-20260829-203602` 为 `10/10` PASS：连续九次原生放入把陈列分数提升到 `124`，超过一等奖阈值 `90`，模拟评审后再原生取回一件；每次均验证菜单入口、互斥锁获取/释放、单次展台变更、库存守恒和评分状态。
- 最新 full snapshot schema 为 `140 required / 124 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 training allowlist / 33 catalogued blocked / 0 Product Executor`。完整回归为 Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_fishing_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-291

- `executor.use_warp_totem` 已闭合五种原版传送图腾 `(O)688/(O)689/(O)690/(O)261/(O)886` 的透明读取、fresh 路由重绑定、动作编译、类型化请求、共享原生库存物品使用和延迟回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认 Farm/Mountain/Beach/Desert/Island 全部可达变体及其消耗语义。
- 透明桥逐库存行发布精确物品身份、公共使用门、2000ms 动画和 1000ms 回调合同，并实时解析 Farm `WarpTotemEntry`/农场类型回退、固定目的地、地图宽度修正、主动节日入口和按顺序应用的被动节日地图替换。节日前误消耗但不传送、联机节日 ReadyCheck、已在精确目的地和基础使用门失败均在上游排除。
- 执行层只调用 `UseInventoryObjectNative`，不直接调用 `Game1.warpFarmer`，不写位置、库存、可见性、无敌或音频。回执要求一个精确图腾 `2->1`、原生首段至少 68 个效果精灵、目的地图/格、节日路由以及角色显示/无敌/移动状态全部结算。
- 隐藏静音 E 盘隔离运行 `runtime-warp-totem-20260829-181523` 五变体 `5/5` PASS：Farm `48,39`、Mountain `31,20`、Beach `20,4`、Desert `35,43`、IslandSouth `11,11` 均由原生回调抵达，每例 `68` 个即时精灵、约 `281 tick` 后完成并恢复控制。
- 最终回归为 Core `1998/1998`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `139 required / 123 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 training allowlist / 34 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `festival.manage_grange_display`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-290

- `executor.use_treasure_totem` 已闭合原版宝藏图腾 `(O)TreasureTotem` 的透明读取、fresh 候选环重绑定、动作编译、类型化请求、共享原生库存物品使用和严格回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认原生效果是中心周围按四舍五入距离 3 生成至多 16 个 `(O)590` 宝藏点，并递增 `TreasureTotemsUsed`。
- 透明桥逐格发布全部 16 个候选、放置/占用/前景层/灌木/可挖或冬季草地门、最终可生成集合、使用前计数和指纹。原生代码中形似 Forest 的判断实际比较物品名 `Treasure Totem`，对基础物品恒为 false；合同保留该真实操作数与结果，不按示例修正源码语义。
- 上游在公共物品门失败、室内或可生成集合为空时直接排除，策略只决定何时及在哪里使用。机械执行器只调用 `UseInventoryObjectNative`，不直接写地图、库存或世界计数；新宝藏点继续交给既有 `ArtifactSpots` 透明读取和 `executor.clear_obstacle` 挖掘链，不建立第二套采集实现。
- 隐藏静音 E 盘隔离运行 `runtime-treasure-totem-20260829-173258` PASS：Farm 中心 `(6,12)` 原生生成 `16/16` 个宝藏点，槽 9 图腾 `2->1`，`TreasureTotemsUsed 0->1`，地点宝藏点 `5->21`。
- 最终回归为 Core `1976/1976`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `138 required / 122 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 training allowlist / 35 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_warp_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-289

- `executor.use_return_scepter` 已闭合原版回城魔杖 `(T)ReturnScepter` 的透明读取、fresh 住宅/落点重绑定、动作编译、类型化请求、原生即时工具调用和严格异步回执。锁定 1.6.15 `Data/Tools` 与 `Wand.cs` 确认其为不可丢失、不可出售、非消耗型 `Wand`，入口必须经过 `Farmer.BeginUsingTool -> Tool.beginUsing(InstantUse) -> Game1.toolAnimationDone -> Wand.DoFunction`。
- 透明桥实时调用 `Utility.getHomeOfFarmer(player).getFrontDoorSpot()`，分别发布房主 `FarmHouse` 与农场工 `Cabin` 的精确门前格，不把主屋常量错误外推给联机角色。住宅缺失、浴衣、桥上、执行器瞬态门禁以及已在精确落点时均在上游排除；全部原生物品槽保持可读且栈前后均为 1。
- 原生时序为 12 个随机烟雾精灵加 17 个横向轨迹精灵、`wand` 音效、1000ms 回调和 2000ms `freezePause`。源码顺序表明 `Wand` 暂时写 `CanMove=false` 后，`Tool.beginUsing` 在返回前将其恢复为 true，实际输入冻结由 `freezePause` 保持；透明合同和运行断言按完整调用栈而非孤立方法体记录。
- 运行层只选择精确 `Wand` 并调用 `BeginUsingTool`，不直接调用 `warpFarmer`，也不写位置、显示、无敌、移动或库存。隐藏静音 E 盘隔离运行 `runtime-return-scepter-20260829-162520` PASS：从 Farm `(66,19)` 原生抵达主屋门前 `(64,15)`，同步观察 29 个精灵，125 tick 后状态结算，原槽 `(T)ReturnScepter` 仍为 stack 1。
- 官方 Wiki 二次确认回城魔杖不消耗、房主回主屋门前、农场工回自己的小屋门前；本地反编译仍是字段、门禁和时序的主证据。
- 最终回归为 Core `1964/1964`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `137 required / 121 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 training allowlist / 36 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_treasure_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-288

- `executor.use_rain_totem` 已闭合原版雨水图腾 `(O)681` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认 `Object.performUseAction -> rainTotem` 的上下文许可、`RainTotemAffectsContext` 路由、默认上下文节日门、天气写入、2000ms 动画与提示对话完整分支。
- 透明桥分别发布“分支判断目标上下文”和“实际天气状态归属上下文”，因此支持模组上下文重定向而不会错记写入对象。默认/沙漠重定向/姜岛分支分别绑定 Default/Default/Island；运行层只调用共享 `UseInventoryObjectNative`，不直接写天气、库存、精灵或音频。
- 官方 Wiki 的季节首日提示触发了源码复核：默认上下文即时写入 Rain 后，换日仍会经过 `Game1.getWeatherModificationsForDate`。透明字段和执行门现绑定明日日期、最终有效天气及有效性；季节首日、开局固定天气、绿雨、夏季固定风暴、主动/被动节庆等覆盖会在消耗前排除。已是 Rain 也在上游排除。
- 隐藏静音 E 盘隔离运行 `runtime-rain-totem-20260829-153640` 为 4/4 PASS：默认、沙漠重定向和姜岛均原生消耗 `2->1` 并写入正确天气实例；节日前分支保持 `stack=2` 并拒绝执行。原生 2000ms 信息对话由输入覆盖关闭，随后通过精确 `Farmer.canMoveNow` 回调恢复控制。
- 最终回归为 Core `1949/1949`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `136 required / 120 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 training allowlist / 37 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_return_scepter`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-287

- `executor.use_monster_musk` 已闭合原版怪兽香水 `(O)879` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认原生入口在 750ms 动画回调中调用 `Object.MonsterMusk -> Farmer.applyBuff("24")`，`BuffManager.Apply` 对同 ID 先移除再替换，不叠加旧时长。
- 透明桥实时发布全部精确库存行、公共物品使用门、Buff 24 数据定义与当前实例剩余时长、1750ms 冻结/750ms 回调/1400ms 后续动画合同，以及普通矿井与火山地牢的双倍怪物生成消费者。读取不推进三枚紫色精灵的随机 X 速度。
- 运行层只调用共享 `UseInventoryObjectNative`，不直接施加 Buff、不广播精灵、不播放音频、不写库存。回执等待 Buff 新实例、600000ms 总时长、精确一次消耗、方向 2 和完整原生冻结结算；已有 Buff 的快照到执行时间只允许最多 5000ms 的单调递减漂移。
- 隐藏静音 E 盘隔离运行 `runtime-monster-musk-20260829-141932` 双分支 PASS：首次施加库存 `2->1`，刷新分支 `1->0`；两次均观测到新 Buff 24 实例，刷新把约 `599968ms` 恢复到约 `599984ms`。
- 最终回归为 Core `1932/1932`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `135 required / 119 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 training allowlist / 38 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_rain_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-286

- `executor.use_horse_flute` 已闭合原版马笛 `(O)911` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、原生 `Object.performUseAction` 与严格延迟回执。锁定 1.6.15 反编译确认起始与 1500ms 回调各检查一次 `Utility.GetHorseWarpRestrictionsForFarmer`，远程分支再通过 team event、马匹 mutex 和 `Game1.warpCharacter` 完成原生召回。
- 透明桥发布完整库存身份/临时不可见状态、四位限制掩码与错误优先级、召回矩形、全地图拥有马匹的 GUID/位置/骑手/mutex 状态、邻近分支、朝向及延迟合同。指纹绑定玩家位置/朝向、限制、马匹身份与库存可见性；`Utility.findHorseForPlayer` 原生遍历全部已加载/生成地点，不依赖模型猜测马厩位置。
- 运行层只调用 `performUseAction` 和原生调用者的 `reduceActiveItemByOne`；不直接调用 team warp event、不写马匹位置。远程召唤强制朝下并等待精确拥有马匹到达玩家格；已有马在一格邻域内是成功无传送分支并保留原朝向。两条分支都验证可复用马笛堆叠不变。
- 隐藏静音 E 盘隔离运行 `runtime-horse-flute-20260829-120232` 双分支 PASS：透明投影、执行重绑和原生 team event 选中同一马厩马匹；该 GUID 从 `Farm 56,14` 原生召回 `40,7`，随后邻近使用保持 `40,7`，两次库存均为 `stack=1`。
- 最终回归为 Core `1921/1921`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `134 required / 118 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 training allowlist / 39 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_monster_musk`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-285

- `executor.use_firework` 已闭合三种原版烟花 `(O)893/(O)894/(O)895` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享相邻移动、原生放置和严格瞬时回执。锁定 1.6.15 分支确认三者映射 `fireworkType=0/1/2` 与源图 X=`256/272/288`，共用 2400ms 引信和延迟火箭。
- 透明桥只在 full profile 且库存存在烟花时计算当前加载地图合法区间，并发布精确临时精灵占位格。读取阶段不调用 `Game1.random`；只发布火箭 ID `20..30` 与 Y 加速度 `-0.36..-0.27`、步长 `0.01` 的完整结果域。
- 动作严格为 `PlayerCommandOnly`：不进入自主候选或训练 allowlist。运行层只复用 `CanPlaceInventoryObjectNative -> PlaceInventoryObjectNative`，不直接广播精灵、播放音频或修改库存；执行回执要求 5 个新原生精灵、目标格 2 个主体精灵、精确三色身份、随机结果域和单物品消耗全部成立。
- 隐藏静音 E 盘隔离运行 `runtime-firework-20260829-111607` 三分支 PASS；观测火箭 ID 为 `22/20/25`，加速度为 `-0.27/-0.34/-0.31`，均在反编译域内。
- 最新 full snapshot schema 为 `133 required / 117 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 training allowlist / 40 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_horse_flute`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-284

- `executor.read_secret_note` 已闭合普通秘密纸条 `(O)79` 与日记残页 `(O)842` 的完整透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生物品使用和严格回执。锁定 1.6.15 反编译确认普通纸条用 `Utility.CreateRandom(gameId, playerId, unseenCount * 777)` 从原生未读顺序确定性抽取，日记残页取最小未读编号。
- 透明桥发布完整 `Data/SecretNotes` 目录、原文与 SHA-256、已读集合、每类未读原生顺序、选择结果、图片/文本菜单预期以及 10/23 号纸条的任务副作用。菜单投影新增 `secret_note_image` 和 `which_bg`，输出不再只凭运行时返回值判断。
- 书籍和纸条现共用唯一 `UseInventoryObjectNative` 调用原生 `performUseAction` 并仅在成功时执行原生调用者的 `reduceActiveItemByOne`；纸条执行器不直接写已读集合、任务、菜单或库存。菜单为空是产品安全门，不伪装成原版纸条分支条件。
- 隐藏静音 E 盘隔离运行 `runtime-secret-note-smoke-20260829-103012` 四分支 PASS：多未读集合确定性选择 18、10 号新增任务 30、23 号新增任务 29、1001 号日记残页无任务副作用，四例均精确已读、原生 LetterViewerMenu 和单物品消耗。
- 最新 full snapshot schema 为 `132 required / 116 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 training allowlist / 41 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_firework`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-283

- `executor.plant_grass` 已闭合普通草种 `(O)297` 与蓝草种 `(O)BlueGrassStarter` 的透明读取、精确地块重绑定、动作编译、v1 请求绑定、共享相邻移动、原生放置和后状态回执。锁定 1.6.15 反编译确认两条分支分别创建 `Grass(1,4)` 与 `Grass(7,4)`，均播放 `dirtyHit`。
- 上游用途/布局规划器必须提供 `grass_layout_reason` 和精确目标/站位；执行器不会推断牧草布局。编译器从 fresh full snapshot 重绑草种变体、槽位、堆叠、合法区间、可达性和投影指纹，任一漂移均失败关闭。
- 生产运行只调用共享 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem`，不直接写 `terrainFeatures`、`grassType`、`numberOfWeeds` 或库存。`current_location.terrain_features` 现直接发布草类型与 weeds 数，支持严格输出复核。
- 隐藏、静音、E 盘隔离运行 `runtime-grass-placement-20260829-093605` 双分支 PASS：type 1/type 7 均为四株初始 weeds，库存精确减一，透明后状态一致。
- 最新 full snapshot schema 为 `131 required / 115 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 training allowlist / 42 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.read_secret_note`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-282

- `world.tune_drum_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音色回执。锁定 1.6.15 反编译确认 `(O)463` 每次右键将 `preservedParentSheetIndex` 按 `(current + 1) % 7` 推进，并播放 `drumkit0..6` 对应音色。
- 该动作严格为 `PlayerCommandOnly`，默认策略候选和训练 allowlist 均排除。路过播放由独立 `farmerAdjacentAction` 入口处理，不得替代调音。Drum 与 Flute 只共用一个 `NoteBlockTuning` 移动/原生交互状态机，各自保留独立透明字段、验证规则和 E3 证据。
- 隐藏、静音、E 盘隔离运行 `runtime-drum-block-20260829-040623` 为 PASS：`6->0`、`drumkit0`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 40 training allowlist / 43 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.plant_grass`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-281

- `world.tune_flute_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音高回执。锁定 1.6.15 反编译确认 `(O)464` 每次右键按 `0..2400`、步长 `100`、共 25 档推进，特殊边为 `2300->2400->0`。
- 该动作严格为 `PlayerCommandOnly`。编译器选择空槽或工具槽以禁用手持物音色覆盖，执行一次原生 `GameLocation.checkAction`；路过触发的 `farmerAdjacentAction` 是独立播放入口，不得混入调音命令或训练。
- 隐藏、静音、E 盘隔离运行 `runtime-flute-block-20260829-034718` 为 PASS：`2300->2400`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 40 training allowlist / 44 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `world.tune_drum_block`，继续复用同一共享移动/交互骨架并单独锁定七档鼓声音色循环。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-280

- `farming.read_farm_computer_report` 已闭合透明读取、显式玩家候选、DailyPlan、机械字段新鲜重绑定、动作编译、v1/v2 类型请求、原生延迟对话和报告摘要回执。锁定 1.6.15 反编译确认原生 `(BC)239` 以对象所在地点的 `GetRootLocation()` 为报告根，精确读取作物、空闲耕地、成熟/未浇水作物、温室、采集物、已完成机器、干草与农场洞穴状态。
- 透明桥直接发布结构化报告字段及本地化原生报告 SHA-256；策略层无需打开菜单才能获知这些信息。该动作严格为 `PlayerCommandOnly`，仅显式玩家请求能生成，默认候选和训练 allowlist 均排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement`，到站后只调用一次 `GameLocation.checkAction`，等待原生 500ms 延迟生成 `DialogueBox`；不合成报告、不直接设置菜单，并验证对象身份、精确报告摘要和工具栏槽恢复。显式命令完成后保留报告供玩家阅读。
- 隐藏、静音、E 盘隔离运行 `runtime-farm-computer-20260829-031326` 为 PASS：`native_handled=true`、即时 `shakeTimer=500`、`freezePause=500`、延迟菜单为 `DialogueBox`，报告摘要与透明投影一致，且只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 40 training allowlist / 45 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。
- 下一语义切片固定为 `world.tune_flute_block`：先实时反编译音高调整、输入方向与持久化语义，再确定 PlayerCommandOnly 边界；继续复用共享对象移动/交互，不复制第二套执行器。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-279

- `movement.use_mini_obelisk` 已闭合透明读取、校准候选、DailyPlan、机械字段重绑定、动作编译、v1/v2 类型请求、原生运行和延迟传送回执。锁定 1.6.15 反编译确认原生分支按 `location.objects.Pairs` 的实际枚举顺序取前两个 `(BC)238`，以 `Vector2.Zero` 为哨兵；从交互站位选择欧氏距离更远的一端，平局取第二端，再按下、左、右、上的顺序选择第一个 `IsTileBlockedBy(All,All)==false` 落点。
- 透明桥逐对象发布实际原生配对序号、两端坐标、每个安全站位对应的目的端与落点。第三个方尖碑、非基础对象、缺失配对、无落点和破坏性对象陷阱均在上游失败关闭；编译器从新鲜快照重绑全部机械字段，不能接受模型伪造的配对、目标或落点。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 与共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产文件不写 `Farmer.Position`、不调用 `setTileLocation`；它等待原生 50ms 延迟回调完成，并以精确落点、两端引用/身份不变、角色重新显示、菜单为空和工具栏槽恢复验收。
- 隐藏、静音、E 盘隔离运行 `runtime-mini-obelisk-20260829-011139` 为 PASS：`native_handled=true`，预期/实际落点均为 `(1,2)`，配对坐标和对象引用不变，只加载 TransparentBridge 与 RuntimeTestHarness。该动作严格为 `ExecutorCalibration`，不会进入默认策略候选或训练 allowlist，运行样本范围为 `executor_calibration_only_not_strategy_desire`。
- 最新权威对账为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 40 training allowlist / 46 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1863/1863`、Backend `134/134`。
- 下一语义切片固定为 `farming.read_farm_computer_report`。先实时反编译原生 Farm Computer 分支及数据来源，再决定候选/训练分类；不得把“能读取报告”直接解释成策略收益，也不得复制第二套对象移动器。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-277 / EVD-278

- Issue #31 的“按共享原生底层批量推进”已采纳，但批量只表示复用同一移动/菜单状态机；每个语义动作仍保留独立透明字段、前后条件、运行夹具和 E3 证据，禁止把一条运行结果外推给整组动作。
- `Lantern` 与 `Raft` 均确认为原版不可达的删减工具，并转入显式兼容占位目录。事实纠正：锁定 1.6.15 的 37 项 `Data/Tools` **包含 Lantern、没有 Raft**；二者均只有残留类型/调试构造或兼容状态，没有正常存档获取路径。它们不进入原版语义分母、候选、训练和待实现列表，但占位保留原生类型与未来 MOD 适配入口。
- `animals.collect_auto_grabber_contents` 已闭合透明读取、上游排除、DailyPlan、机械字段重绑定、动作编译、原生菜单运行和物品守恒回执。它与喂食斗只共享 `NativeObjectInteractionMovement`；收取本身走精确 `(BC)165 -> ItemGrabMenu -> receiveLeftClick`，生产代码不直接改背包或 held Chest。
- 隐藏、静音、E 盘隔离运行 `runtime-auto-grabber-20260828-165346` 为 PASS：原生菜单转移 `2` 个堆栈、共 `5` 件物品，Auto-Grabber 余量 `0`，`native_handled=true`，对象/Chest 身份和选中槽均保持正确。
- 最新权威对账为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 40 training allowlist / 47 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1848/1848`、Backend `132/132`。
- 下一语义切片为 `movement.use_mini_obelisk`，继续处理 `Object.checkForAction` 组；只复用已验证的移动/交互底层，并独立锁定成对方尖碑、目标选择、传送后位置与新鲜快照终止条件。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-275 / EVD-276

- `StardewValley.Tools.Raft` 已确认为锁定 1.6.15 的不可达删减类型。类、`Farmer.isRafting` 和兼容移动分支仍存在，但 37 项 `Data/Tools` 没有 Raft，全部运行时内容资产也没有获取或事件入口，源码除类型自身 `GetOneNew` 外没有外部构造或工厂路径。当前分类已由上方 EVD-277 接管为分母外兼容占位；`executor.use_raft` 不进入候选、训练或原版待实现目录。
- `animals.withdraw_feed_hopper_hay` 已闭合透明读取、上游候选、DailyPlan、机械字段重绑定、动作编译、原生运行和守恒回执。透明投影精确发布根地点料仓干草、动物数、动物屋容量、已摆干草、未喂动物、原生取草量、背包接纳和安全站位；没有未喂动物、料仓为空、料槽无容量或背包不能接纳时在上游排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 和共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产代码不直接写 `piecesOfHay`、不直接加背包；成功必须同时满足料仓精确减少、背包 `(O)178` 精确增加、喂食斗身份不变、菜单为空和选中槽恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-feed-hopper-20260828-130723` 为 PASS：原生一次取出 `8`，料仓 `10 -> 2`、背包 `0 -> 8`，`native_handled=true`，喂食斗保留且槽位恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 training allowlist / 49 catalogued blocked / 0 Product Executor`；原生分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。回归为 Core `1844/1844`、Backend `131/131`。
- 下一语义切片按冻结待办顺序为 `animals.collect_auto_grabber_contents`；必须先核对原生菜单、互斥锁、容器库存和空容器分支，再复用唯一库存/菜单转移实现，不复制第二套容器执行器。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-274

- `world.play_singing_stone` 已闭合透明读取、显式玩家指令候选、DailyPlan 表述、机械字段重绑定、动作编译、原生运行与回执。锁定 1.6.15 反编译确认目标是基础 `StardewValley.Object` `(BC)94`，不是同名家具 `(F)1300`；原生分支执行 `Game1.random.Next(2400)` 后向下取整到百位，均匀产生 `0..2300` 共 24 种 `crystal` 音高，并把 `shakeTimer` 设为 `100`。
- `Game1.random` 是共享 RNG，透明桥只发布完整分布和 `unavailable_shared_rng_state_not_consumed`，不得读取、推进或猜测下一音高。候选只选择一个精确声音石；对象身份、相邻站位、安全工具栏槽、分布和原生契约全部由最新快照与编译器重绑定，坐标漂移时不得替换另一块石头。
- 该动作严格为 `PlayerCommandOnly`：默认候选、自主日计划和策略训练全部排除，只有 `InvocationSource=PlayerCommand` 且显式确认后可进入已有授权链。运行证据的范围是 `player_command_only_executor_evidence`，不能反推训练准入。
- 运行时与 House Plant 共用 `NativeObjectInteractionMovement`，保留原生动画/BFS，只调用一次 `GameLocation.checkAction`；生产代码不调用 `Game1.playSound`、不写 `shakeTimer=100`、不消费 RNG，并在交互前双检四向物体陷阱、目标身份、安全手持状态和槽位恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-singing-stone-20260828-102438` 为 PASS：`native_handled=true`、`shake_timer=100`、`item_id=94`、`qualified_item_id=(BC)94`，选中槽恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 权威对账为 `148 registered / 199 semantic / 147 compiler-bound / 74 five-gate / 38 training allowlist / 51 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。全量回归为 Core `1839/1839`、Backend `130/130`。
- `executor.use_raft` 已从动作全集分母移除：锁定 1.6.15 虽保留 `Raft` 类、`Farmer.isRafting` 和移动兼容分支，但 37 项 `Data/Tools` 无该工具，源码也无获取、工厂、事件或外部构造入口；当前以分母外兼容占位保留原生证据，不作为候选、训练目标或原版待实现能力。
- 下一语义切片固定为 `animals.withdraw_feed_hopper_hay`：先锁定饲料斗交互、库存/料槽容量与取草数量分支，再判断是否完全复用唯一库存转移引擎；不得从历史段落回退到已闭合声音石或复制第二套转移系统。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-272 / EVD-273

- `farming.collect_slime_ball` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与产物守恒回执。锁定 1.6.15 反编译确认自然史莱姆球必须是精确 `SlimeHutch` 中的基础 `StardewValley.Object`、`(BC)56`、`Fragility=2`；`CheckForActionOnSlimeBall` 先移除对象，再按 `DaysPlayed`、`uniqueIDForThisGame` 和目标格生成 `10..20` 个 `(O)766`，并按 `NextDouble()<0.33` 的几何分布生成 `(O)557`。
- 透明桥在 `current_location.objects[].slime_ball_collection` 发布来源类型、永久身份、随机种子、两类精确预期产量、原生返回值、生成契约、共享 debris 拾取交接和安全相邻站位。候选仅选择“收取哪一个球”；种子、产量、站位、空槽和原生调用细节均由编译器在新鲜快照上重绑定，指定坐标漂移时不得改选另一个球。
- 生产执行器复用共享 BFS 和通用破坏性四向物体陷阱检查，临时切到真实空工具栏槽位，只调用一次 `GameLocation.checkAction`。它不直接删对象、不直接造 debris，也不复制拾取器；回执以“背包 + 当前地点 debris”的 `(O)766`/`(O)557` 守恒量验证生成，剩余地面物品继续交给唯一 `executor.pickup_debris`。
- 隐藏、静音、E 盘隔离运行 `runtime-slime-ball-20260827-174122` 为 PASS：目标 `(2,3)` 原生移除，种子投影与实际守恒输出均为 `(O)766 x11`、`(O)557 x0`，工具栏槽恢复，只加载 TransparentBridge 与 RuntimeTestHarness。
- EVD-273 新增调用来源硬分类。`PlayerCommandOnly` 动作可以保留透明读取、编译和原生执行能力，但默认候选生成会排除，策略来源会被 SafetyPolicyGate 阻断，训练准入会给出 `PlayerCommandOnly` 类型化排除。当时包括 `buildings.change_skin`、`buildings.paint`、`world.rotate_house_plant`、`executor.change_building_skin`、`executor.place_furniture`、`executor.set_sign_display_item`、`executor.edit_text_sign`；只有 `InvocationSource=PlayerCommand` 且满足原有确认/权限门时才能执行。
- 权威对账为 `147 registered / 199 semantic / 146 compiler-bound / 73 five-gate / 38 training allowlist / 52 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`。本切片复用 `current_location.objects`、`current_location.debris` 和 `player.inventory`，不新增 full 快照顶层必需字段。
- 最终回归为 Core `1833/1833`、Backend `129/129`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片按未闭合目录顺序为 `world.play_singing_stone`；必须先锁定原生声音石交互和可观测结果，再决定它属于自主策略、机械日计划还是 `PlayerCommandOnly`，不得由“能执行”反推训练准入。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-271

- `world.rotate_house_plant` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与八帧回执。模型只选择“轮转哪一盆”这一有意义的装饰目标；当前贴图帧、预期帧、相邻站位、空工具栏槽位、恢复槽位和原生调用契约全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译与 `Data/BigCraftables` 确认：`(BC)0..7` 都是基础 `House Plant`，永久 `ItemId/QualifiedItemId` 与当前 `ParentSheetIndex` 相互独立。`CheckForActionOnHousePlant` 通常执行 `0→1→…→7→0`；但通过真实 `GameLocation.checkAction` 且空手交互时，起始帧 `7` 的第一次对象调用变为 `0` 并返回 `false`，地点层随后再次调用对象，所以一次玩家交互的最终结果是 `7→1`。
- 透明桥在 `current_location.objects[]` 中分别发布永久身份、当前帧、一次原生地点交互后的精确帧、预期对象调用次数、地点返回值与邻格。只有精确基础对象、`Crafting` 类型、`0..7` 有效帧、可达邻格、菜单关闭且存在真正空工具栏槽位时才开放候选；工具槽回退被拒绝，以冻结空手双调用语义。
- 生产执行器复用共享 BFS，临时切到编译器绑定的空槽，只调用一次 `GameLocation.checkAction`，然后恢复原槽位。生产文件不写 `ParentSheetIndex`，也不直接调用对象 `checkForAction`；成功必须同时观察预期帧、同一对象引用、永久 ID 不变、菜单为空和槽位恢复。
- 该装饰动作是严格 `PlayerCommandOnly`：不会进入默认候选、自主日计划或策略训练；只有玩家显式指令并通过原有确认门后，现有编译与原生执行链才可运行。
- 原生 `Object.checkForAction` 还存在四个基数方向均被不可通行对象封闭时对目标调用 `performToolAction(null)` 的破坏性前导分支。透明桥为每个站位发布 `object_trap_blocked`，候选在上游排除，运行时在寻路前和交互前再次核对并失败关闭，绝不让装饰轮转误删目标。
- 隐藏、静音、E 盘隔离运行 `runtime-house-plant-20260827-123652` 为 `8/8 PASS`：起始帧 `0..7` 分别得到 `1,2,3,4,5,6,7,1`，最后一例验证对象调用次数为 `2`；每例 `item_id=0`、`qualified_item_id=(BC)0` 与选中槽位均保持/恢复。
- 权威对账更新为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 training allowlist / 53 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`，blocking 均为 `0`。full 快照顶层因本切片复用 `current_location.objects`，仍为 `130 required / 114 readable / 16 contextual / 0 blocking`。
- 最终回归为 Core `1826/1826`、Backend `128/128`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片固定为 `farming.collect_slime_ball`：必须先核对史莱姆球的生成条件、每日状态、原生收取产物与背包/掉落分支，再决定复用对象交互还是库存转移内核。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-270

- `rewards.claim_statue_blessing` 已闭合透明读取、无参数日级候选、DailyPlan、机械字段重绑定、动作编译、原生运行与全天 buff 回执。小模型只输出“领取今日祝福”这一语义目标；祝福编号、雕像、站位、日期、天气/节日分母与对象交互全部由最新快照和编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfBlessings` 需要 `StatKeys.Mastery(0) >= 1`，并以 `CreateDaySaveRandom(DaysPlayed*777)` 丢弃八次后抽取。普通日为 `Next(7)`；雨天或节日为 `Next(6)`，因此不会抽到蝴蝶祝福。`hasBeenBlessedByStatueToday` 或任一 `statue_of_blessings_*` buff 都会在上游排除重复领取。
- 透明投影覆盖七种原生效果：速度 `+0.5`、幸运 `+1`、体力不下降、前三次钓鱼减难度、交谈友谊 `20 -> 60`、暴击率 `+0.1`、17:00 前棱彩蝴蝶及其金钱/棱彩碎片结算。效果仍由原生 Buff、Farmer、BobberBar、NPC、GameLocation 和 Butterfly 分支消费，没有复制战斗、钓鱼或社交执行器。
- 生产执行器复用共享 BFS，到精确相邻格后只调用一次 `GameLocation.checkAction`。禁止生产路径直接 `applyBuff`、改写 `hasBeenBlessedByStatueToday` 或写 `AppliedBuffs`；结果同时要求唯一预测 buff、日锁为真、菜单为空。
- 隐藏、静音、E 盘隔离运行 `runtime-statue-blessing-20260827-115317` 返回 `applied/verified`：当天普通日预测并实际得到 `statue_of_blessings_1`，只观察到该幸运祝福，日领取锁变为 `true`。
- 最新真实 full 快照已安装：`130 required / 114 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 training allowlist / 54 catalogued blocked / 0 Product Executor`，原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终回归为 Core `1813/1813`、Backend `127/127`；Release 解决方案构建保持 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `world.rotate_house_plant`：先反编译八帧轮转、对象子类和交互返回值，再复用当前地图对象定位与原生直接交互；不得把装饰轮转伪装成长线策略，也不得建立第二套移动器。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-269

- `mining.choose_dwarf_statue_power` 已闭合透明读取、两个策略候选、DailyPlan、选择保留与机械字段重绑定、动作编译、原生运行和全天 buff 回执。小模型只从当天两个精确选项中输出 `dwarf_statue_power_id`；雕像、站位、菜单索引、buff ID、日期指纹和点击细节全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfTheDwarfKing` 需要 `StatKeys.Mastery(3) >= 1`，以 `Utility.CreateRandom(DaysPlayed*77, uniqueID)` 每天固定生成两个不同的 `0..4` 选项。五种效果分别落在额外矿石、楼梯/竖井、煤炭、炸弹免伤和晶球概率的原生分支；已有任一 `dwarfStatue_*` buff 时当日不能重选。
- 生产执行器只复用共享 BFS，调用原生 `GameLocation.checkAction` 打开 `ChooseFromIconsMenu`，核对两个图标身份和顺序后调用原生 `receiveLeftClick`。禁止生产路径直接 `applyBuff` 或写 `AppliedBuffs`；普通矿井、骷髅洞和火山继续读取同一个 buff，不新增第二套矿洞或战斗系统。
- 隐藏、静音、E 盘隔离运行 `runtime-dwarf-king-statue-20260827-111935` 对当天两个选项 `0/3` 均返回 `applied/verified`，分别只观察到 `dwarfStatue_0` 和 `dwarfStatue_3`，原生菜单在 800ms 销毁后关闭。
- 最新真实 full 快照已安装：`129 required / 113 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 training allowlist / 55 catalogued blocked / 0 Product Executor`，原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终 Release 回归为 Core `1802/1802`、Backend `126/126`；解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `rewards.claim_statue_blessing`。必须先锁定祝福雕像的每日随机状态、领取锁、全部祝福效果和原生菜单/对象路径；可以复用本切片的对象定位、日级候选和原生菜单生命周期，但不得把两个雕像的随机分母、buff ID 或规则混用。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-267

- `recovery.escape_object_trap` 已贯通透明读取、候选、日计划和动作编译链。小模型只需发出该高层恢复命令；编译器从玩家四个基数方向的对象行与 `farm.machines` 中按固定方向顺序选择首个 `removal_safe_now=true` 的相邻机器，不要求模型生成拆除坐标或工具细节。
- 锁定 1.6.15 反编译确认 `Object.checkForAction` 的四向封闭前导分支会调用目标 `performToolAction(null)`；base `Object` 随即从 `location.objects` 移除且只产生装饰 debris，不返还资产，并可能继续执行目标交互。透明桥发布四向存在性、可通行性、运行时/声明类型、交互状态及该破坏性契约，但 `destructive_native_fallback_enabled=false`。
- 安全恢复只编译到既有 `executor.remove_machine`：复用其精确机器身份、所有权、空闲/无产出/无附件、fragility、Pickaxe、指纹和原生 debris/自动回收验证；没有第二套移除运行时。非四向封闭、菜单/手持物/骑马冲突、无安全相邻机器或机器状态漂移均关闭失败。
- 隐藏、静音、E 盘隔离环境已采集真实 full 快照并安装：`127 required / 111 readable / 16 contextual / 0 blocking`。聚焦候选/日计划/编译/治理测试与全量 Core 回归通过；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 当前权威对账为 `142 registered / 199 semantic / 141 compiler-bound / 68 five-gate / 36 training allowlist / 57 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。five-gate 未增加，因为尚未伪造一份专用四向陷阱运行回执；实际资产移除内核沿用已验证的 EVD-147/EVD-148。
- 下一语义切片固定为 `rewards.claim_pot_of_gold`：先从锁定反编译确定彩虹尽头奖励对象的生成、可领取状态、背包满分支、一次性状态与原生转移路径，再决定复用对象交互/背包转移内核；不把通用拾取冒充特殊奖励领取。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-266

- `recovery.sleep_in_tent` 已作为独立的终端跨日语义闭合；它复用共享移动、原生交互、确认输入、跨日等待、保存/出货结算和稳定世界判定，不并入 `executor.place_tent` 或普通床 `executor.sleep`。
- full 快照新增 `player.temporary_sleep`、`menus.tent_sleep_prompt_context`，并扩展精确 base `Tent` 行的睡眠地点、锚点、规范交互格、规范站位、朝向、问题键和原生许可。编译器冻结这些字段以及运行时身份、血量、可通行性和路径，只允许最后一个计划步骤执行。
- 生产执行器先走到 `anchor+(0,1)`、面朝上，再调用原生 `GameLocation.checkAction` 打开 `SleepTent`，随后发送原生确认输入。它不直接写日期、`sleptInTemporaryBed`、Tent health、玩家位置或 Tent 表；共享跨日结算必须观测 temporary-bed 标志、日期恰好增加一天、同地点同格醒来、保存菜单关闭、标志复位和 Tent 隔夜销毁。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-sleep-20260826-164032` 返回 `applied/verified`：总日数 `222 -> 223`，在 `Farm:66,19` 同地点同格醒来，观测到 `SleepTent_Yes`、temporary-bed 标志先置位后复位、世界稳定且精确 Tent 消失。
- 当时权威对账为 `141 registered / 199 semantic / 140 compiler-bound / 68 five-gate / 36 training allowlist / 58 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `126 required / 110 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从未闭合目录首项 `recovery.escape_object_trap` 开始：先锁定原生脱困分支与透明状态，再判断是否能复用现有移动/交互内核；在反编译和严格状态契约完成前不建立泛化执行器。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-265

- `executor.place_tent` 已作为独立放置语义闭合；它不包含 `recovery.sleep_in_tent`。full 快照的 `player.tent_placement` 仅接受精确 base `(O)TentKit`，逐方向压缩发布原生合法站位，并绑定室外限制、跨季节明日日期、普通节日、被动节日地图替换/多日窗口、3x2 矩形、中心锚点、初始 `Tent` 状态、日更销毁和后续睡眠交接。
- 编译器冻结精确槽位/堆叠/地点/站位/朝向探针/矩形/锚点/日历/投影指纹/布局指标/放置理由与原生契约。共享邻接执行器现会尊重已冻结的 `stand_tile`，再由唯一 `PlaceInventoryObjectNative` 调用 `Utility.tryToPlaceItem -> Object.placementAction`；生产代码不直接写 `largeTerrainFeatures` 或库存。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-placement-20260826-160613` 为 `4/4 PASS`，覆盖方向 `0..3`。每例均验证原生返回、库存减一、精确 base `Tent`、方向派生锚点/3x2 边界、`health=5`、玩家可通行性及下一份透明快照回读。
- 当时权威对账为 `140 registered / 199 semantic / 139 compiler-bound / 67 five-gate / 36 training allowlist / 59 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `123 required / 107 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `recovery.sleep_in_tent`：复用现有移动、交互、确认、跨日等待和 `executor.sleep` 的终止性规则，只新增 `Tent.performUseAction -> SleepTent_Yes -> sleptInTemporaryBed`、同地点醒来及隔夜销毁回执；不得把它重新并入放置动作或普通床睡眠。

## 历史记录 · 2026-08-26 上一权威检查点：EVD-264

- `executor.edit_text_sign` 已作为独立语义闭合，不与标牌放置或展示物赋值合并。full 快照对精确 base `StardewValley.Object` 文字牌发布 raw/display 文本、`showNextIndex`、直接序列化 SHA-256、替换要求、60 UTF-16 code-unit 限制及完整原生菜单管线。
- 编译器严格绑定目标地点/格子/运行时类型/qid/状态哈希/投影指纹、相邻站位、旧文本与覆盖授权，并拒绝超过 60 code units、双引号或控制字符的非原生键盘输入。生产执行器仅复用共享相邻移动，调用 `GameLocation.checkAction`，逐字符输入 `TitleTextInputMenu.textBox` 并点击原生完成按钮；不直接写 `signText` 或 `showNextIndex`。
- 原生回执按实际顺序验证 `NamingMenu.FilterInput -> Utility.FilterDirtyWords -> Trim -> NetString -> TokenParser.ParseText -> Utility.FilterDirtyWords`，并验证 `showNextIndex == string.IsNullOrEmpty(SignText)`。隐藏、静音、E 盘隔离运行 `runtime-text-sign-editing-20260826-104822` 为 `5/5 PASS`，覆盖首次写入、旧文本替换、首尾空白裁剪、清空、中文 UTF-16 输入和 60 code-unit 边界。
- 当时权威对账为 `139 registered / 199 semantic / 138 compiler-bound / 66 five-gate / 36 training allowlist / 60 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、三类 blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 标牌三条独立链现已全部闭合：`executor.place_sign`、`executor.set_sign_display_item`、`executor.edit_text_sign`。下一语义切片固定为 `executor.place_tent`，继续复用共享原生物品放置与布局安全，不建立第二套移动或放置系统。

更新时间：2026-08-26

## 历史记录 · 2026-08-25 当时权威检查点：EVD-263

- `executor.set_sign_display_item` 已闭合：full 快照对每个精确 base `Sign` 发布全部非空背包物品、源对象直接序列化 SHA-256、原生展示类型、旧展示载荷和替换要求；读取端禁止调用 `getOne()`，避免构造副本时消耗 RNG。
- 编译器逐项绑定目标、相邻站位、源槽位/身份/品质/堆叠/状态哈希、旧载荷和替换授权。生产执行只复用共享相邻移动并调用 `GameLocation.checkAction -> Sign.checkForAction`，不直接写 `displayItem`、`displayType` 或背包。
- 隐藏、静音、E 盘隔离运行 `runtime-sign-display-item-20260825-170751` 为 `6/6 PASS`：覆盖展示类型 `1..5` 与非 Object 默认分支，后五例覆盖已有展示替换；源物品引用、数量和完整序列化状态全部不变。
- 当时权威对账为 `138 registered / 199 semantic / 137 compiler-bound / 65 five-gate / 36 training allowlist / 61 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、blocking `0`，KnowledgeCompiler 为 `585/585`。
- 下一语义切片固定为 `executor.edit_text_sign`：独立闭合原生 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex`，不得并入标牌放置或展示物赋值。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-262

- `executor.place_sign` 已闭合实时标牌目录、精确库存、当前地图原生合法格、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。锁定 1.6.15 存在两条不同原生分支：带 `sign_item` 标签的三种展示牌生成精确 `StardewValley.Objects.Sign`；`(BC)TextSign` 生成精确 base `StardewValley.Object`。两者均只放置空牌且库存精确 `-1`。
- `player.sign_placement` 从实时 `Game1.bigCraftableData` 枚举全部 `4` 行，不硬编码标牌 ID 或数量。候选绑定数据行、运行时类型、分支、当前地图合法格、邻接站位、布局安全、空载荷预期和拓扑指纹；`current_location.objects[].sign_state` 回读展示物类型/身份、文字、`showNextIndex`、可通行性和运行时支持状态。
- 生产执行器只复用 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem -> Object.placementAction` 与既有 `PlaceInventoryObjectNative`，不直接写 `location.objects`、标牌载荷、文字或库存。隐藏、静默、E 盘隔离运行 `runtime-sign-placement-20260825-160744` 对实时目录全部 `4/4 PASS`，覆盖三种展示牌和文字牌两条分支。
- 反编译复核同时修复动作分母遗漏：`checkForAction` 与 `Objects/Sign.cs` 已进入扫描，新增 Object/Sign 动作面及 `20` 个原生分支。全部分支已逐项映射到已有能力或显式 `catalogued_blocked` 语义，不以通用交互伪装成完整支持。当前分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。
- 当时权威对账为 `137 registered / 199 semantic / 136 compiler-bound / 64 five-gate / 36 training allowlist / 62 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `122` 个状态因子，其中 readable `106`、contextual/stale `16`、blocking `0`；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.set_sign_display_item`，只实现 `Sign.checkForAction` 的展示物绑定和逐字段回执；其后独立实现 `executor.edit_text_sign` 的 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex` 语义。两者不得并入摆放动作。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-261

- `executor.place_furniture` 已闭合实时 `Data/Furniture` 全目录、精确库存家具、当前地图家具拓扑、严格编译、共享移动、共享原生物品放置和逐字段回执。普通落地家具进入 `location.furniture`；在空桌面放置的 1x1 家具进入该桌子的 `heldObject`，两种原生终点不得混淆。
- `player.furniture_placement` 发布实时目录中的 `645` 行，并只对当前已加载地图执行目的限定的原生合法格扫描。每个候选绑定运行时子类、地点限制、墙面锚点修正、矩形占地、可通行性、所有虚拟旋转状态、原生终点、空桌身份和拓扑指纹。`current_location.furniture` 回读相同身份、旋转、占地、碰撞、容器内容与桌面载荷。
- 读取探针禁止调用可能改变源家具旋转状态的 `Furniture.getOne()`；统一使用 `Furniture.GetFurnitureInstance` 创建脱离源对象的规范实例。每个候选格都必须重新创建探针并调用 `InitializeAtTile`，防止旧 bounding box 污染后续格子和桌面判断。生产执行器只调用虚拟 `rotate()` 与既有 `PlaceInventoryObjectNative`，不直接写 `currentRotation`、`location.furniture`、桌面载荷或库存。
- 隐藏、静默、E 盘隔离运行 `runtime-furniture-placement-20260825-101457` 从实时目录选择完整代表集并 `25/25 PASS`：覆盖 `Furniture`、`StorageFurniture`、`FishTankFurniture`、`BedFurniture`、`RandomizedPlantFurniture`、`TV` 六种规范运行时子类，家具类型 `0..17`，旋转步数 `0..3`，以及 `location_furniture` 与 `table_held_object` 两种终点。
- 当时权威对账为 `136 registered / 184 semantic / 135 compiler-bound / 63 five-gate / 36 training allowlist / 48 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `121` 个状态因子，其中 readable `105`、contextual/stale `16`、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.place_sign`。先反编译锁定标牌身份、显示物绑定和交互/文字语义是否与普通物品放置重叠；复用既有共享移动和唯一原生放置内核，只新增标牌特有的透明字段、严格校验与回执，不建立第二套放置系统。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-260

- `executor.place_flooring` 已闭合实时地板目录、当前地图合法区间、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。库存源必须是精确 base `StardewValley.Object`；原生 `Object.placementAction(IsFloorPathItem)` 在 `terrainFeatures` 中生成精确 base `StardewValley.TerrainFeatures.Flooring`，生产执行器不直接写地形表、视图或库存。
- `player.flooring_placement` 从实时 `Game1.floorPathData` 与 `Flooring.GetFloorPathItemLookup()` 发布完整目录。只有当前已加载地图按地板身份压缩原生合法区间，跨图必须到达后重新绑定；这保留全量数据透明，同时避免无关日程扫描所有持久地图。每个区间绑定同类八邻接掩码；`Random` 连接只绑定完整 `whichView=0..15` 结果域，非随机构造值为 `0`。
- 原生放置拒绝任何已有 `TerrainFeature` 的目标格，不存在放置时替换地板的能力。拆除仍是独立 Axe/Pickaxe/damage 工具语义。`Flooring.isPassable` 恒为 true，因此共享布局校验要求目标、邻接站位位于当前 BFS 可达域，放置前后可达格计数相同；不得错误复用围栏的虚拟阻塞规则。
- 隐藏、静默、E 盘隔离运行 `runtime-flooring-placement-20260825-015659` 从实时目录枚举 13 个规范物品并 `13/13 PASS`。全部返回 `applied/verified`，透明回读验证 data key、base Flooring、同类邻接掩码、可通行、视图域与库存减一；随机 `(O)415` 本轮实际视图为 `5`。
- 当时权威对账为 `135 registered / 184 semantic / 134 compiler-bound / 62 five-gate / 36 training allowlist / 49 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `119` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_furniture`。必须先反编译区分普通家具、壁挂/地毯/旋转、室内地点限制与原生 `Furniture.placementAction`，继续复用共享移动和原生物品放置内核，不得复制第二套放置系统。

更新时间：2026-08-25

## 历史记录 · 2026-08-22 当时权威检查点：EVD-258

- `executor.load_crab_pot_bait` 已闭合透明读取、严格编译、共享邻接移动、原生 `GameLocation.checkAction` 执行和逐字段回执。它是独立的蟹笼上饵 primitive，不并入通用机器投料，也不复制 EVD-209 的蟹笼收取链。
- full 快照对每个精确 base `CrabPot` 发布生命周期、owner/Luremaster 状态和背包内所有 `Category=-21` 饵料的槽位、堆叠、运行时类型、品质、单位状态哈希及原生 probe 结果。编译器绑定目标、邻接站位、owner before/after、饵料身份、理由与原生契约；任一漂移即失败关闭。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-bait-20260822-164015` 为 5/5 PASS：普通、豪华、万能、魔法和特定目标鱼饵均由原生 checkAction 装入，原生 `reduceActiveItemByOne` 精确消耗一件，下一份透明快照读回相同 qid、运行时类型、品质、单位状态哈希与 owner。
- 当时权威对账为 `133 registered / 184 semantic / 132 compiler-bound / 60 five-gate / 36 training allowlist / 51 catalogued blocked / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_fence`。必须先审查并复用现有物品放置、邻接移动、碰撞与布局安全实现，禁止形成第二套放置系统。

更新时间：2026-08-22

## 历史记录 · 2026-08-22 当时权威检查点：EVD-257

- `executor.place_crab_pot` 已闭合透明读取、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。源物品与放置结果均为 `(O)710`，但库存中的普通 `StardewValley.Object` 必须经原生 `Object.placementAction` 转成由当前玩家拥有的精确 `StardewValley.Objects.CrabPot`。
- `player.crab_pot_placement` 在 full profile 中枚举每个背包蟹笼、所有已加载持久地点的原生合法水格区间，以及每个区间的鱼区、栖息地、垃圾概率、Mariner/Luremaster 修正、原生顺序捕获行和生产签名。编译器绑定精确库存、地点、水格、邻接站位、拓扑指纹、生产签名、放置理由和原生契约；运行时再次执行原生合法性检查。
- 放置复用唯一 `PlaceInventoryObjectNative` 与既有邻接移动；后续收取继续复用已经由 EVD-209 闭合的 `fishing.collect_crab_pots -> executor.collect_crab_pot`，没有第二套蟹笼收取、移动或物品放置系统。该放置 primitive 保持 calibration/evaluation-only，上层策略负责布局、产能与放置理由。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-placement-20260822-152201` 返回 `applied/verified`：目标 `Farm:73,31` 生成精确 base `CrabPot`，owner、空饵料、空产出、未 ready 与库存减一均通过；下一份透明快照读回同一蟹笼及淡水生产签名 `|0.2|freshwater|721,716,722`。
- 复核 EVD-209 与锁定反编译时发现，原冻结分母遗漏了独立的原生上饵动作：`CrabPot.performObjectDropInAction` 接受 `Category=-21` 饵料并写入 owner/bait，它不属于通用机器输入链。现已把 `executor.load_crab_pot_bait` 作为 `catalogued_blocked` 显式补回分母，未伪装成已实现。
- 当时权威对账为 `132 registered / 184 semantic / 131 compiler-bound / 59 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一语义切片先闭合 `executor.load_crab_pot_bait`，再进入 `executor.place_fence`。

更新时间：2026-08-22

## 历史记录 · 2026-08-22 当时权威检查点：EVD-256

- `executor.place_cookout_kit` 已闭合透明读取、严格编译、共享邻接移动、原生物品放置和逐字段回执。锁定 1.6.15 的源物品是 `(O)926`，`Object.placementAction` 落地的是 `StardewValley.Torch` / `(BC)278`，并带有 `Fragility=1`、`destroyOvernight=true`；两者不得混作同一个物品身份。
- `player.cookout_kit_placement` 在 full profile 中枚举背包内每个野炊工具和所有已加载持久地点的原生合法区间、布局指纹、当日生命周期及烹饪交接契约。编译器必须绑定精确槽位、堆叠、地点、落点、邻接站位、投影指纹、放置理由和原生契约；运行时仍会重新调用 `Utility.playerCanPlaceItemHere`。
- 机器、储物和野炊工具现在共用唯一 `PlaceInventoryObjectNative` 内核；野炊工具放置后由既有 `player.cooking` 立即识别为 `cookout:location:tile` 来源，没有复制烹饪、移动或布局系统。该底层 primitive 保持 calibration/evaluation-only，只有上层同日烹饪目的才能授权消耗。
- 隐藏、静默、E 盘隔离运行 `runtime-cookout-kit-placement-20260822-105210` 返回 `applied/verified`：库存 `1 -> 0`、目标生成精确 `(BC)278`、当日销毁标志为 true，且透明烹饪端点交接成功。
- 当时权威对账为 `131 registered / 183 semantic / 130 compiler-bound / 58 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `116` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一切片从 `executor.place_crab_pot` 开始，先审查是否能复用现有蟹笼读取、收取、物品放置和水域边界实现。

更新时间：2026-08-22

## 历史记录 · 2026-08-18 当时权威检查点：EVD-255

- `executor.apply_tree_treatment` 已完成透明读取、严格编译、共享 BFS 邻接移动、原生物品放置与逐字段回执。锁定 1.6.15 的真实语义是 `(O)419` 醋永久禁止一棵树长苔，不是 `(O)805` 树肥；原生分支没有成长阶段限制。
- 隐藏静默隔离运行 `runtime-tree-treatment-20260818-162145` 已验证 `has_moss true -> false`、`stop_growing_moss false -> true` 和醋堆叠减一。生产执行器只调用 `Object.placementAction`，直接树字段写入仅存在于测试夹具。
- 该项保持 executor calibration / evaluation-only，不进入自主候选。上层以后必须提供 `tree_treatment_reason` 和策略授权，不能因为背包有醋、地图有树就自动安排永久处理。
- 当时机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照仍要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从重生成后的 `catalogued_blocked` 清单选择；优先检查 `executor.place_cookout_kit` 是否应复用现有物品放置、布局安全与烹饪来源投影，禁止复制机器/储物放置或野炊烹饪执行器。

更新时间：2026-08-18

## 当前权威检查点（优先于下方历史记录）

- EVD-254 已闭合 `crafting.forge_item`：完整快照发布全部已加载原生锻造来源、实时背包工具/戒指与已装备戒指输入、原生碎片成本/返还、统计变化、精确确定性输出及 Diamond/Dragon Tooth 完整随机结果域。显式单次意图经唯一 DailyPlan/队列链进入 `executor.forge_item`，生产执行器只使用原生 `ForgeMenu` 输入与按钮并等待 1600 ms 生命周期。隐藏静默隔离运行 `runtime-forge-20260818-122957` 的九个操作族全部返回 `applied/verified`。
- EVD-253 已闭合 `crafting.cook_recipe`：完整快照发布所有已学配方在每个实时厨房/野炊工具来源上的精确材料消费顺序、主冰箱与原生枚举顺序的小冰箱拓扑、互斥锁、齐氏调味料、输出品质/订单标记和历史烹饪次数。显式单次烹饪意图经唯一 DailyPlan/队列链进入 `executor.cook_recipe`；普通制作、工作台和烹饪共用一个原生 `CraftingPage` 配方点击辅助函数，但厨房保留独立的容器、锁和 `recipesCooked` 语义。隐藏静默隔离运行 `runtime-cooking-20260817-202809` 对厨房银星煎蛋和野炊工具普通煎蛋均返回 `applied/verified`。
- EVD-252 已闭合 `animals.manage_animal`：透明桥发布精确动物、原生查询许可、繁殖、售价、当前家园和兼容家园；显式意图经唯一 DailyPlan/队列链进入 `executor.manage_animal`。隐藏静默隔离运行 `runtime-animal-management-20260816-012959` 对首次抚摸后改名、繁殖开关、搬家和确认出售四支均返回 `applied/verified`。生产执行器只发送原生动物交互和 `AnimalQueryMenu` 点击，不直接写动物或金钱字段；过宽名字在确认前阻塞。
- 当前机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。

- 锁定版本仍为 Stardew Valley 1.6.15；KnowledgeCompiler 当前为 `585/585` exports、blocking `0`。
- 动作对账当前为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；
  原生分母仍为 `320 surfaces / 428 branches / 150 map tokens`，三类 blocking 均为 `0`。
- EVD-248 已闭合 `buildings.construct` 的第一个严格范围：模型必须明确给出建筑类型、目标地点和建设理由；
  透明桥从实时 `Game1.buildingData` 与全部原生可建地点读取基础蓝图、Builder、条件、价格、材料、现有与在建
  建筑、服务动作和原生合法落点。候选在上游排除缺意图、条件/资源/落点漂移、在建冲突和材料预留冲突，
  再经 DailyPlan 汇入 EVD-236 已存在的唯一 `executor.construct_building`。隐藏静默隔离运行分别验证了
  无任务策略建造 `runtime-quest-terminal-daily-plan-20260812-105048` 和原 `HaveBuildingQuest` 回归
  `runtime-quest-terminal-daily-plan-20260812-105331`，均由原生 Robin/`CarpenterMenu` 建造 `Farm` 上的
  `Coop`，核对钱、材料、坐标和三天倒计时。证据不外推到 Wizard、升级、换皮或长期建筑策略。
- 最新 full 快照覆盖 `114` 个必需字段，blocking `0`；KnowledgeCompiler 仍为 `585/585`、blocking `0`。
- EVD-246 已闭合 `mining.use_elevator`：透明桥读取玩家最深/当前矿层、入口 `Action=MineElevator`、
  楼层 `Buildings/mine` 索引 112 和精确 `MineElevatorMenu` 条目身份；DailyPlan 复用既有跨图移动、
  `interact` 与 `close_menu`。运行时只点击原生端点和菜单，不直接调用 `enterMine`/`warpFarmer`，并跨帧
  验证最终位置。隐藏静默隔离矩阵 `runtime-mine-elevator-20260812-004601` 为 2/2：25 层回入口及入口回
  25 层均通过。`mining.reach_depth` 仅在实时端点存在且已解锁检查点能推进最终目标时复用该链，并保留
  最终深度为 continuation；其余楼层继续使用原 current-floor planner。
- EVD-245 已闭合 `mail.process_letter`。共享解析器覆盖锁定 `Data/mail` 的 179 封信和 107 条指令，
  解析阻塞为 0；透明桥公开原生顺序队列、玩家实际拥有的邮箱位置、附件容量上界和完整
  `LetterViewerMenu` 状态。DailyPlan 只组合既有移动、`interact` 与 `close_menu`，运行时只发送原生
  菜单输入并核对附件、任务、特别订单和星之果实收据，没有第二套移动/菜单执行器，也不直接写钱、
  配方、任务或最大体力。严格隐藏静默矩阵
  `artifacts/runtime-mail-processing/runtime-mail-processing-20260811-221959/summary.json` 为 5/5；
  新 full 快照为 required 107、blocking 0，KnowledgeCompiler 为 585/585、blocking 0。
- 下一主切片按动作对账中剩余语义依赖继续选择；普通矿井电梯已经闭合，不得再建立平行实现。采石场
  金镰刀洞窟、Skull Cavern 和火山继续保持独立身份，不得混入普通矿井电梯。
- `quest.advance` 的 28 个目录阶段为 `24 bound / 0 blocked / 3 observation-only / 1 native-unreachable`；反编译扫描为
  `12` 种普通任务类型和 `9` 种特别订单目标类型，未发现未登记类型。
- EVD-235 已把任务终端矩阵扩展为 `4/4`：新增普通 `CraftingQuest`，从目的限定的
  `player.quest_crafting` 经 `quest.advance`、DailyPlan、动作队列和既有原生 `CraftingPage` 执行器完成，
  并写入精确任务身份、前后存在/完成状态及 terminal 事实。EVD-236 又闭合普通 `HaveBuildingQuest`：
  `player.quest_building_construction`、候选、DailyPlan 和唯一 `executor.construct_building` 通过原生
  Robin/`CarpenterMenu` 放置建筑，原生扣除钱和材料并生成三天施工倒计时；后续天数复用既有恢复睡眠链。
- `quest.advance` 已因目录零阻塞提升为 `Declared / StepCompilerDeclared / RegisteredOnly`，但没有进入训练白名单；
  目录绑定完成不等于原生运行证据闭环。
- EVD-237 已确认秘密物品取得不是独立任务动作：原版只有任务 128/129，`Railroad.getFish` 在同一
  原生钓鱼事务中返回 `(O)191` 并创建两条任务；透明桥与现有 `fishing.catch_fish` 已覆盖该特殊收获，
  `itemFound=false` 任务行只是事务中的瞬态观察，不得再建第二套任务钓鱼执行器。候选与运行时均复用
  `player.inventory_capacity` 要求至少一个空格；满包必须先走既有存储转移链，避免唯一项链进入未接管的
  `ItemGrabMenu` 后无法重新取得。
- EVD-238 已关闭 type-11 假缺口：锁定 `Data/Quests` 的 66 行没有 Weeding 类型，`Quest.getQuestFromId`
  没有对应工厂分支，原生任务源码没有任何 `questType=11` 写入点；它只是保留的兼容常量。目录使用独立
  `native_unreachable` 状态，KnowledgeCompiler 每次对账复核常量、工厂分支和写入点；旧存档或模组强塞
  type-11 时明确失败关闭，不生成除草执行器。
- EVD-239 已闭合 Junimo Kart 分数的静态主链：真实 full 快照验证
  `current_location.arcade_action_tiles` 可读且带来源；`JKScoreObjective` 绑定 Saloon 街机，复用移动、地图交互和
  `MinecartGame/Endless` 对话原语和唯一 `executor.play_junimo_kart`。经 EVD-307 更新后，所有 AI actor 的默认策略均为 `timed_equivalent`：按既有
  15 分钟平均预算计时 54,000 tick，运行时可加速墙钟，但只通过原生 `MineCart.submitHighScore()` 提交并核对
  `JKScoreObjective`，结果必须标记 `simulated_equivalent`，不得伪装成原生完美游玩证据。
- 原有只发送跳跃输入的 `native_perfect` 控制器完整保留且与等价分数写入隔离；它后续只用于玩家明确触发的完美存档代打。
  只有该模式真实达到 Endless 50,000 分并自然提交，才能登记 Junimo Kart 原生五门证据。等价模式可服务训练、联机陪玩和专用房主，
  但不得冒充原生完美证据；独立 `minigame.play_junimo_kart` 高层项继续后置，不阻塞其他游戏能力。
- 2026-08-11 复核发现 `30,190` 历史运行的 smoke 脚本没有设置 `SMAPI_MODS_PATH`，实际还加载了
  `JunimoTestClient`，因此该制品只保留为受污染诊断样本，不再作为运行验收或回退基线。脚本现使用每次运行独立的
  两模组白名单，并把白名单写入汇总。首个干净矩阵为
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-002951/`，峰值 `10,940/50,000`，仍为 blocked。
- 当前唯一控制器已加入 `Bubble` 和 `FallingBoulder`/下一次 spawner 的只读轨迹预测，复刻落石原生加速度、
  速度上限和逐轨道反弹顺序；干净矩阵证明这些实体进入运行轨迹。连续跳跃模拟现复刻原生释放时重力归零、
  位移前 `x/x+4/x-4` 落地检测、坡道/冰面/黏液速度倍率和落地帧水平位移。运行
  `runtime-junimo-kart-20260811-011601` 观察 57 次落地，预测与实际 X 的最大绝对误差为 `0px`。
- `native_perfect` 当前干净最高分仍为 `10,940/50,000`；精确落点矩阵峰值为 `9,320`，其中 7/8 次为 theme 0，不能直接作为
  算法回退判断。剩余主缺口是 8 次 planner fallback：需要从原生轨道求下一段可行落地区间，替代固定
  `gap + 18px` 目标，并按主题进行可重复校准。不得用直接改分、改轨道或改任务目标替代控制问题。
- EVD-240 已完成 `quest.accept_daily`：透明桥读取实时 `questOfTheDay`、接受许可、原生任务身份和从 Town
  地图发现的 `Billboard 3` 入口；候选层先做上游许可排除，再按新快照逐连接器接近柜台，终端编译为原生
  Billboard 交互与接受。隐藏静默隔离运行 `runtime-daily-quest-acceptance-20260811-125209` 已验证任务进入
  quest log、`acceptedDailyQuest=true`、原生接受字段和 `daysLeft=2`。新快照基线为 required 103、blocking 0。
  该高层项及原语暂保持 RegisteredOnly，不因一次通过直接进入训练白名单。
- EVD-241 已完成统一的 `quest.accept_special_order` 链：Town、Qi 和沙漠节庆入口共享透明读取、滚动寻路、
  原生开板和精确左右选择，不另建移动或菜单系统。Town 隐藏静默隔离运行
  `runtime-special-order-acceptance-20260811-172636` 已通过，验证原生互斥锁延迟、`Robin2` 的 key/seed/指纹和
  accepted type；新快照基线为 required 104、blocking 0。Qi 与沙漠节庆目前只有反编译和结构覆盖，待独立运行
  校准，因此两项仍保持 RegisteredOnly，five-gate 与 allowlist 不变。
- EVD-242 已完成 `quest.claim_reward`：透明桥实时枚举普通任务日志中的可领取金钱奖励，并用任务 ID、运行时类型、
  标题、奖励、接受日和 daily 标记生成稳定指纹；候选层在菜单非空、身份或金额漂移时上游排除。唯一
  `executor.claim_quest_reward` 构造原生 `QuestLog`、选择精确任务行、点击 `rewardBox`，验证原生
  `OnMoneyRewardClaimed`/`OnLeaveQuestPage` 收据；生产代码没有直接写钱、`moneyReward`、`destroy` 或任务日志。
  隐藏静默隔离运行 `runtime-quest-reward-claim-20260811-195512` 验证 `144755 -> 145505`、奖励 750g 和任务移除。
  最新 full 快照基线为 required 105、blocking 0。
- 最新运行证据：
  `artifacts/runtime-quest-reward-claim/runtime-quest-reward-claim-20260811-195512/summary.json` 和
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-194648/summary.json`。

## 当前阶段

锁定 Stardew Valley 1.6.15 的动作全集对账和独立分母冻结已经完成，现已转入逐动作纵向
闭环。当前 114 个注册项是可复用的实现基线，不是被废弃的旧代码；63 个已编目未注册语义项
用于记录已证实但尚未实现的能力。正式训练保持阻塞。

## 已完成

- 现有注册表、治理表、编译器注册和 Harness 能力表已做集合一致性校验；
- 取消 `OptionRegistry` 中手写的 31/65 固定计数，改为逐 ID 对账；
- 每个现有动作已归属唯一主执行引擎；
- KnowledgeCompiler 开始生成：
  - `native-action-surface-inventory.json`
  - `native-action-branch-inventory.json`
  - `native-map-interaction-coverage.json`
  - `semantic-action-catalog.json`
  - `action-implementation-reconciliation.json`
  - `action-progress-dashboard.json`
  - `native-action-denominator-fingerprint.json`
- 独立冻结文件 `native-action-denominator-freeze.json` 已与当前指纹核对一致；覆盖状态和
  实现进度不进入分母身份哈希，因此后续把占位动作提升为实现不会伪造“分母变化”；
- 原生方法扫描已改为 Roslyn 语法解析，方法重载按完整签名独立建档；
- 60 个宽入口已展开为 428 条带源码行号和哈希的分支证据；
- 1,102 个有效地图交互实例已归并为 150 个 Action/TouchAction token，并逐项连接
  到原生处理分支。

## 当前任务

当前生成基线位于 `catalogs/vanilla-1.6.15/`：

- 320 个原生输入表面，表面级未分类 0；
- 60 个宽入口全部生成分支目录，428 条分支中待语义审查 0、缺注册 0；
- 150 个地图交互 token 中 142 个映射到语义动作，8 个经原生分支证实为无玩家语义、
  失效/遗留静态 token，待审查 0；
- 语义动作目录共 177 项：114 项已有 `OptionSpec`，63 项为
  `catalogued_blocked`，确认存在但尚未登记的动作数为 0。

机器状态为 `native_action_denominator_frozen`，当前锁定扫描范围已闭合并通过独立审批文件
核对。不能把“已登记”解释为“已实现”：现有代码的编译器孤儿 0、运行 ID 孤儿 0；
Product Executor 仍为 0；EVD-204 回填后，五门证据闭环为 8，训练准入为 7。

## 退出条件

- 原生动作表面未分类数为 0；
- 宽入口分支和有效地图交互 token 未审查数为 0；
- 锁定扫描范围的语义动作分母可确定性重生成并完成治理冻结；
- 所有已证实语义动作均已注册，未实现项必须保持显式 blocked；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

首个缺口 `inventory.transfer_item` 已完成纵向闭环：强类型明确意图、透明库存图投影、
上游候选、路径站位、日计划展开和既有 `executor.transfer_material` 原语复用均已接通。
EVD-192 在 E 盘隔离存档中验证了“箱子到玩家”和“玩家到箱子”两个方向，均经原生
`Chest`/`ItemGrabMenu`、逐单位右键、互斥锁释放、before/after 数量差分和训练记录；往返后
箱子数量恢复，过期源栈投影在菜单打开前失败关闭且零点击。该项五门证据已登记，可进入其
明确意图范围内的训练；Product Executor 仍未集成，不得把 Harness 闭环称为产品陪玩闭环。
`player.storage_crafting` 与 `player.storage_placement` 的透明性 join 已修复：旧快照稳定复现
94 项 required state factor 中 2 项缺失，当前实时快照为 77 项带完整来源可读、17 项场景性
不可用、0 项阻塞。新安装器会先校验全部 required factors、哈希与版本，再原子更新外部权威
字典的 current 指针；完整 KnowledgeCompiler 已以该指针达到 585/585 exports、blocking 0。
复核确认 `recovery.stabilize_day` 的全部当前候选到日计划/队列编译链早已完成，普通社交对话和
送礼也共用唯一 `executor.social_interact` 原生 Harness 执行器，不得重复实现第二套。EVD-195 已在
隐藏、静音的 E 盘隔离存档中闭合 `Farm@2200 -> 单连接器回家 -> 新鲜快照重规划 -> 原生睡眠 -> 新日`，
因此 recovery 五门已通过，但它仍是校准型高层动作，不进入策略训练。EVD-196 又通过现有社交链
完成 Abigail 的实时远端滚动追踪：35 轮中 31 个连接器动作和一次原生送礼均验证通过，普通礼物
`(O)388` 严格由栈 1 变为 `null`，且未读取未来日程。`social.gift_npc` 现仅在“当前已加载原版 NPC、
同图或滚动连接器追踪、普通单件礼物”范围内五门闭合并进入训练准入；不得外推为模组 NPC、特殊
物品或全部社交完成。下一切片按既定路线重建准入策略轨迹并接入 C# 结构化排序器，不重做候选、
编译器或社交执行器。

正式轨迹与数据治理硬闸已经完成：`PolicyTrainingAdmissionFilter` 直接消费生成式 allowlist，
校准行与未准入行分开计数；每条 v2 轨迹绑定 effective ranking、完整源候选、版本化状态特征、
编译队列、执行结果、fresh after-state 和观察型长回报。清洗器按存档/游戏日确定性切分，拒绝
冲突标签并生成逐文件 SHA-256 manifest。旧 `BaselineFeatureRowTrainer` 仍只作聚合烟测；正式路径
使用下节的 C# 结构化提供器。

## 历史记录 · 2026-08-02 正式模型链状态

EVD-201 已完成首个真实 C# 结构化策略提供器。轨迹 schema 升级为
`policy_decision_trajectory.v2` / `policy_features.v2`，每条轨迹除版本化状态特征外，还保留
完整源候选对象，商店、位置、物品、价格、开放时窗、排程、原因、参数与结构化效果字段不会在
训练前丢失。训练器只对“已准入且当时可用”的候选建立成对比较，检查点绑定数据清单及三个分区
SHA-256、特征/候选/能力/字典/编译器/执行器版本；推理只重排既有候选，不复制候选生成、日计划、
编译器或执行器。`--require-structured-policy` 在检查点缺失时失败关闭。

当前标准 E 盘生产轨迹、跨度观测、正式 manifest 和检查点四个路径均不存在。因此完成的是模型
基础设施与合成契约验收，不是生产训练。直接当时后续计划仍是按权威字典依赖顺序扩大五门准入范围，
再用真实、verified/fresh 的长期 rollout 生成 v2 轨迹和闭合跨度标签；形成正式 manifest 后才运行
`StardewAI.PolicyModel`。只有独立存档评测与第三年 21 分长跑通过后，才冻结“最强完美 AI”基线。

EVD-202 没有新增第二套矿洞候选、编译器或执行器，而是把已有 EVD-106 运行证据登记到
`mining.obtain_skull_key` 的五门：范围严格限定为普通矿井 119 -> 120 层、原生骷髅钥匙宝箱领取、
`has_skull_key false -> true` 与原生退出。该目标现进入训练白名单；沙漠矿洞、采石场矿洞金镰刀和
火山矿洞仍是独立族，未被本证据放行。当时五项准入为 `inventory.transfer_item`、
`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、`social.talk_npc`。

EVD-203 随后独立登记 `volcano.reach_caldera`：EVD-190 的火山 0..9 -> Caldera 完整原生滚动链
记录 106 步、82 次 applied/verified 和 24 次安全重规划；EVD-191 的战斗目的链记录 66 步、
27 次验证动作和 39 次安全脱离/重规划。两份制品均无非新鲜快照或未变化状态。模型只决定是否选择
“到达 Caldera”这个高层目标；浇岩浆、清石、战斗、移动、门和连接器仍由确定性候选/编译/执行链
完成。普通矿井、沙漠矿洞、采石场矿洞金镰刀与火山矿洞证据继续互不借用。当时六项准入为
`inventory.transfer_item`、`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、
`social.talk_npc`、`volcano.reach_caldera`。

EVD-204 复核并登记 `skills.read_books`。能力目录此前只识别动作队列直接编译器，漏记了现有
`DailyPlanCompiler` 的 `read_inventory_book -> executor.read_book -> wait_ticks` 展开，因此错误显示
`compiler=unbound`；现已用同一治理入口登记日计划 option compiler，没有新增读书执行器。EVD-124
七用例矩阵覆盖六类原版基础书籍分支，全部 applied/verified。当前 compiler-bound 为 77，五门闭环
为 8，训练准入为 7；新增准入项是 `skills.read_books`。自定义 `performUseAction`、畸形模组标签和
原版证据范围外分支继续失败关闭。

## 历史记录 · 2026-08-11 职业选择闭环（EVD-244）

`skills.choose_profession` 已从恢复链中的隐式自动处理提升为正式语义动作。透明桥同时公开当前
`LevelUpMenu` 的两个原生职业选项、精确 ID、标题、描述，以及玩家持久职业列表和待处理升级列表；候选层只在
两个选项均完整可读时开放，并把同一选择界面的两个候选登记为互斥决策。DailyPlan 只把所选职业编译到既有
`close_menu -> executor.close_menu`，没有新增第二套职业或菜单执行器。

锁定版反编译确认 5 级分支为每技能 `skill * 6 + 0/1`，10 级分支按已有 5 级职业选择 `+2/+3` 或 `+4/+5`；
运行回执记录选择前后的职业、待处理升级、生命、体力等即时变化。隐藏静默 E 盘全矩阵
`runtime-profession-choice-20260811-203159` 覆盖原版 30 个职业 ID，修正前置即时 perk 的战斗复核矩阵
`runtime-profession-choice-20260811-203610` 为 6/6 通过，并验证 Fighter `+15` 与 Defender `+25` 最大生命。
最新 full snapshot schema 为 105 required、88 实时带来源可读、17 场景性、blocking 0；动作对账为
114 registered / 177 semantic / 113 compiler-bound / 63 catalogued-blocked，five-gate 为 40，训练准入为 27。

直接下一主切片是 `mail.process_letter`。`mining.use_elevator` 必须先按普通矿井既有移动、菜单和楼层切换链做
复用对账，只有证明存在未覆盖的原生分支才允许新增实现。

## 禁止事项

- 不把 97 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

## 历史记录 · 2026-09-01 当时权威检查点：EVD-327

- Product bootstrap 隐藏静音运行 `product-bootstrap-20260901-013441` 已在 E 盘 Spring 4 隔离副本上通过；3 条真实 Product v2 轨迹全部被数据集接收，前两条分别含 107/143 个准入候选。
- 首个结构化 bootstrap checkpoint 已由 3 条轨迹和 248 个比较对生成，执行器版本固定为 `product_executor.v1`。validation/test 仍为 0，所以它只是正式循环初始权重，不是训练质量结论。
- 正式 launcher 已新增 manifest 级 `save_slot` 与存档文件校验，并显式传递自动加载环境变量；正式进程不能再停在主菜单却被计为训练。
- Core `2193/2193`、Backend `166/166` 全量回归通过；校准结束后 SMAPI 与 8765/8767/8768/8795 均无残留监听。
- 当时后续计划固定为：种入标准正式轨迹路径，prepare/launch 同一份 `training_run_manifest.v2`，完成一个真实游戏日的 Product 训练并验证 checkpoint/hash 更新；之后部署到 119 的全量训练实例，再补非训练动作。
## 历史记录 · 2026-08-12 当时权威检查点：EVD-249

- `buildings.change_skin` 已完成透明读取、上游候选、DailyPlan、共享动作队列、类型化请求、原生 Robin/CarpenterMenu 执行与严格回执。
- 隔离运行 `runtime-quest-terminal-daily-plan-20260812-122957` 为 `applied/verified`：Pet Bowl 默认皮肤通过一次最短 `next` 切换到 `Stone Pet Bowl`，返回 ScienceHouse，并验证三组油漆颜色重置为默认。
- 当时动作对账：122 registered、180 semantic、121 compiler-bound、49 five-gate、32 training allowlist、0 Product Executor；320/428/150 原生分母 blocking 均为 0。
- 当时 full 快照 required 112、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1663/1663、Backend 121/121。
- 下一切片为 `buildings.paint`。它必须复用现有 Robin 服务、Carpenter 建筑选择和菜单退出链，只新增实时颜色参数、上游外观意图约束和 `BuildingPaintMenu` 原生滑杆/回执，不得新增平行建筑菜单执行器。

## 历史记录 · 2026-08-12 当时权威检查点：EVD-250

- `buildings.paint` 已完成透明读取、上游许可、DailyPlan、共享动作队列、类型化请求、原生 `BuildingPaintMenu` 控件与严格回执。
- 透明桥公开每栋可涂装建筑的一至三区域、原生 H/S/L 范围、当前值、默认标志、权限、Robin 服务入口，以及 284 像素滑杆的精确鼠标可达整数集合；上游拒绝不可达、无效果和默认显示值无法解除默认标志的目标。
- DailyPlan 生成 `paint_building_region`，但动作队列继续映射到唯一 `executor.change_building_skin`。共享 `ActiveBuildingAppearanceChange` 复用 Robin、Carpenter、建筑选择和退出生命周期，只在子菜单内部按冻结参数分流；不存在第二套 Robin 状态机。
- 隐藏静音隔离运行 `runtime-quest-terminal-daily-plan-20260812-133245` 已通过：Farmhouse `Building` 区域原生点击到 H180/S37/L-30，目标精确匹配，另外两区域保持默认，训练行落盘。
- 当时为 123 registered / 180 semantic / 122 compiler-bound / 57 catalogued-blocked；full snapshot 113 required、96 带来源可读、17 场景性、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1666/1666、Backend 121/121。
- 下一切片应从剩余 57 个 `catalogued_blocked` 动作中，按权威字典依赖、已有机械引擎复用和可形成严格原生回执的顺序选择；不得以动作数量为理由复制执行系统。

## 历史记录 · 2026-08-12 119 展示循环检查点：EVD-251

- 119 展示房间已改为版本化发布，继续使用原存档、原版联机房主、公开 UDP 24642 和既有可加入小屋；正式训练仍保持关闭，展示循环只运行目的受限的已实现候选集。
- `daily` 快照现有精确域校验；无手持物品以可读空字符串表达，不再把“没有 ActiveObject”误判为字段不可用。队列只执行首个阻塞项之前的连续可执行前缀，控制层 HTTP 超时不早于执行器预算。
- 无窗口房主通过原生 `IInputSimulator` 消费现有移动租约，不写坐标、不注入动作键；通用移动 BFS 避开可移除障碍，清障继续由唯一 `executor.clear_obstacle` 原语负责。
- 锁定版反编译确认多人 `Game1.shouldTimePass()` 由 `netWorldState.IsTimePaused` 决定。专用原版 AI 房主只在原生事件、菜单与显式暂停门槛均允许时清除残留联网暂停位，不直接写 `timeOfDay`。119 实测 58 秒内 `08:20 -> 09:40`，最终版本再次实测 `06:50 -> 08:10`。
- 服务器闭环先连续形成 6 条 applied/verified/fresh 执行记录；修复后无实际候选时只生成空队列并指数退避，不再以 `recovery:refresh_plan_after_stabilization` 制造永久 `wait_ticks` 样本。建筑施工中的等待仍由 `quest.advance` 在透明状态明确为 `construction_in_progress` 时局部复用。
- 既有晚间恢复链在 119 完成 `22:00 -> 回家/睡觉 -> NewDay`。三项服务为 server healthy、planner healthy、host-ai running，透明 `daily` 快照完整且 unavailable 0；这证明展示日循环主体成立，不等于完整动作全集、Product Executor 或正式训练完成。
- 本地最终回归：Core 1671/1671、Backend 122/122、Release solution build 0 errors；保留 1 个既有 `AvoidNetField` 警告。
- EVD-254 已完成上述 `crafting.forge_item` 切片，未建立第二套移动、库存或菜单系统。下一主开发切片为 `executor.apply_tree_treatment`：复用既有树木定位、移动、站位和工具/物品交互基础设施，先按锁定 1.6.15 原生分支明确适用树种、物品消费、成长状态变化、许可与严格回执，再决定其高层目的归属；不得把底层处理动作直接当作策略候选。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-317

- `rewards.claim_adventure_guild_reward` 已完成透明读取、自治正收益候选、DailyPlan、fresh 编译重绑、类型化传输、共享 BFS、原生 Gil 交互、可选对话、整批奖励菜单点击和严格回执。小模型只输出无参数的“领取已获得奖励”，整批身份和所有机械字段由当前快照绑定。
- 锁定版反编译确认 `gil()` 会在一次交互中枚举全部已完成且未领取的怪物讨伐目标，并在奖励菜单出现前立即安排全部邮件/标志副作用。因此上游必须用克隆背包和原生 `Utility.addItemToThisInventoryList` 证明整批容量，不能把目标拆成逐条领取，也不能等下游菜单卡住再恢复。
- 隐藏静音 E 盘运行 `runtime-adventure-guild-reward-20260831-023014` 为 3/3：伪造批次指纹拒绝、背包容量漂移拒绝、真实原生批次领取成功。成功用例新增一个 `Gil_<goalId>`、收取一个精确物品并使 pending 归零；生产代码不直接写背包、邮件、标志或击杀统计。
- 当时 full 快照为 165 required / 148 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 214 registered / 223 semantic / 213 compiler-bound / 138 harness dispatch / 137 five-gate / 56 training allowlist / 9 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2138/2138、Backend 155/155、Release 0 warnings / 0 errors。原生分母为 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0，冻结指纹为 `10b8329f92466d34ce5570679fea0096b298a36693668e8ff107be1794804902`。
- 下一主切片是 `rewards.claim_prize_ticket`。先锁定 `PrizeTicketMenu` 的批次、随机性、容量、菜单与副作用语义，再复用现有路线、菜单输入和收据基础设施。`minigame.play_junimo_kart` 按既有决策继续暂缓，不得在下一轮误当主切片。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-318

- `rewards.claim_prize_ticket` 已完成透明读取、上游许可、DailyPlan、两阶段 fresh 重规划、类型化传输、共享 BFS、原生地点交互、原生 `PrizeTicketMenu` 点击和严格回执。模型只输出无参数的“领取当前奖券奖励”；票券来源、当前奖励等级、精确奖励指纹、地点、站位、库存或 debris 结算均由当前快照和编译器负责。
- 锁定版反编译确认：持有 `(O)PrizeTicket` 时直接前往 ManorHouse `PrizeMachine`；仅有 `specialOrderPrizeTickets` 待发数量时，先在 Town 的 `SpecialOrdersPrizeTickets` 原生端点领取一张实体票券，然后必须用新快照重规划。兑换只领取 `currentPrizeTrack[0]`，背包可接收时进入库存，否则生成原生 debris；随后票券减一且 `ticketPrizesClaimed` 加一。等级 `0..21` 和之后的 9 级循环均已冻结。
- 隐藏静音 E 盘运行 `runtime-prize-ticket-reward-20260831-121025` 为 6/6：伪造投影拒绝、Town 待发票券领取、等级 0、升级房屋等级 5、满背包等级 21 debris、等级 22 循环均通过。生产执行器不直接写票券、奖励、统计或背包。
- 本轮运行还暴露并修正了透明观察器副作用：Cooking、Forge、SpecialOrderBoard、Building 与 FarmHouse 上下文只读取已经加载的地图/床/入口状态，不再因完整快照访问 `Map` 或房屋资产懒加载而改变运行环境。异常多人房屋组合也能生成完整只读快照。
- 当时 full 快照为 166 required / 149 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 216 registered / 224 semantic / 215 compiler-bound / 139 harness dispatch / 139 five-gate / 57 training allowlist / 8 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2145/2145、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `7327c9af60be86fc79d7aff82a33af4ad09ca0aa58398d2bebed1affa5ae6f67`。
- 下一主切片是 `skills.claim_mastery`。先锁定五种精通领取资格、菜单入口、星级/经验结算、奖励与一次性标志，再复用现有路线和菜单输入链。`minigame.play_junimo_kart` 继续暂缓；不得把 8 个显式 blocked、0 Product Executor 或尚未开始的正式全量训练描述为完成。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-319

- `skills.claim_mastery` 已完成透明读取、五分支战略候选、DailyPlan、跨图 fresh 续接、类型化传输、共享 BFS、原生 `MasteryCave.checkAction`、原生 `MasteryTrackerMenu.mainButton` 和严格回执。小模型只在当前可领取的技能中选一个；路线、碑牌、站位、奖励和菜单操作全部由编译器与执行器展开。
- 锁定版反编译确认入口要求五项基础技能均达到 10；MasteryExp 阈值依次为 10000/25000/45000/70000/100000。五块碑牌分别对应耕作、钓鱼、采集、采矿和战斗，领取会原生增加所选 `mastery_N` 与 `masteryLevelsSpent`，授予精确配方/直接物品，满背包时直接物品落为 debris；战斗额外设置一个饰品槽，第五块触发完成态。
- 隐藏静音 E 盘矩阵最终连续两轮 6/6：`runtime-mastery-claim-20260831-130708`、`runtime-mastery-claim-20260831-130839`。覆盖伪造投影、耕作直接物品、钓鱼满背包 debris、采集双配方、采矿双配方、战斗饰品槽和第五碑牌。生产执行器不直接写精通统计、配方、背包、饰品槽或终局状态。
- 首轮 5/6 暴露了无关掉落物数量进入全局指纹导致的误阻塞。修正后全局指纹只锁技能/碑牌/点数结构，所选分支的奖励基线继续由独立 option 指纹严格锁定；这不是放宽状态校验。
- 当时 full 快照为 166 required / 149 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 218 registered / 225 semantic / 217 compiler-bound / 140 harness dispatch / 141 five-gate / 58 training allowlist / 7 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2154/2154、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `c5ba5f114d07060305187ab7266e26ae13c8b24fb2ed9177d5f06f86b4fbe1f4`。
- 下一主切片是 `social.emote`。先锁定原生表情候选、触发权限、多人可见副作用和玩家指令/自治边界，再复用现有社交定位及输入链。`minigame.play_junimo_kart` 继续暂缓；正式全量训练仍未开始。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-320

- `social.emote` 已完成实时透明读取、显式玩家指令候选、DailyPlan、fresh 编译重绑、类型化传输、原生聊天输入和严格回执。小模型/玩家只提供准确表情 key、理由与确认；机械层生成并输入完整 `/emote <key>`，不需要路线，也不复制第二套社交执行器。
- 锁定版反编译确认 `Farmer.EMOTES` 恰有 22 项，其中 `blush/taunt/music/jar` 四项隐藏；透明桥实时发布顺序、显示键/名称、图标、方向、逐帧动画、raw favorites、无副作用推导的八项 effective defaults、performed 状态、selector 可见性和全部 `CanEmote`/聊天/菜单/小游戏门槛。锁定目录顺序或隐藏集合漂移时整体失败关闭，读取侧禁止调用会补写默认值的 `GetEmoteFavorites()`。
- 生产运行只激活原生 ChatBox、逐字符输入并调用 `ChatBox.textBoxEnter`，后续由 `ChatCommands.Emote -> Farmer.netDoEmote -> doEmoteEvent -> performPlayerEmote` 负责联网与结果；禁止直接调用后三者或写 `performedEmotes`。本地回执核对 performed entry 及准确图标/动画；远端可见性归原生网络事件所有，不伪造远端收据。
- 隐藏静音 E 盘矩阵 `runtime-player-emote-20260831-145638` 为 23/23：一项伪造投影被拒，22 个原生表情逐一成功。两项 Option 都是 `player_command_only`、需显式确认，并从默认自主候选与策略训练中排除；训练 allowlist 保持 58。
- 当时 full 快照为 168 required / 151 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 220 registered / 226 semantic / 219 compiler-bound / 141 harness dispatch / 143 five-gate / 58 training allowlist / 6 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2162/2162、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `d145570835f06f8ffb14460ce1107950a4aed243939ed60ee08c583bc17a97e9`。
- 下一主切片固定为 `social.watch_movie`。先反编译锁定购票/邀请/放映/同伴反应/奖励与一次性状态，再判断哪些选择属于模型、哪些机械流程归既有社交/商店/菜单执行层。`minigame.play_junimo_kart` 继续暂缓；正式全量训练仍等待剩余动作、Product Executor 与长线验收。

## 历史记录 · 2026-09-05 当时权威检查点：r32 round07

- 部署时提交 `e0a6221` 把跨日完成门改为必须观察到 `newDaySync.hasSaved` 且 `SaveGame.IsProcessing=false`；`af432ed` 修正透明桥把隔夜系统对话误报为睡眠确认框的问题，并仅用原生输入推进安全的非角色隔夜对话。远端整合后的公开 `main` 等价提交分别为 `bcc0bc0`、`d881555`。
- Core `2273/2273`、Backend `171/171`、Release solution build 为 0 warnings / 0 errors。
- 119 上的 `train.server.20260905.r32.plan07` 在并发 1 下完成 3 次外层迭代：1 次主动作 verified，2 次原生存档边界推进，Summer 2 → Summer 3；SMAPI 日志明确记录 `SaveGame.Save() completed without exceptions.`。
- 存档树 SHA-256 从 `7822c135afa09a355fbed3ce1462784d1551fdf8cfdf81ae4efebd95fcba31a3` 变为 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b`，训练事务为 `committed_after_native_save_boundary`，canonical 制品已更新。
- 正式数据集为 accepted 200 / rejected 0，train / validation / test = 142 / 5 / 53，train pairs = 4367；checkpoint SHA-256 为 `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`。
- 完整证据已归档到 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r32-round07-20260905-011000`；远端与本机 146 / 146 文件逐一 SHA-256 一致。服务器上的正式训练、游戏与执行器进程随后已停止，既有观察隧道不属于训练进程。
- round05/round06 均保持 `staged_not_committed`，没有更新 canonical：前者暴露保存完成判定过早，后者暴露 Summer 2 地震隔夜对话。它们是失败诊断证据，不得计为训练成功轮次。
- 该结果只解除“正式事务无法跨原生存档边界提交”这一硬阻塞，不代表全量训练或完全体陪玩完成。当时后续计划是以 Summer 3 存档和上述 canonical 哈希创建下一份有界计划，继续真实 Product rollout；每轮必须在退出前确认无未解决 Product pending、原生存档已持久化、数据集/checkpoint/manifest 哈希一致并完成本机归档。

## 历史记录 · 2026-09-05 当时权威检查点：r33 round09

- round08 在执行动作前被 `formal_checkpoint_dataset_binding_mismatch` 拒绝。根因是 round07 把 staging 数据文件复制到了 canonical，但 manifest 和 checkpoint 内容中的绝对路径未重绑定；该轮未启动 Product Executor/训练循环，也未修改 canonical，7 个失败控制制品已完成本机 SHA-256 归档。
- `7b1da8d` 修复事务提升：所有 manifest digest 路径必须位于 staging 或 canonical 数据根，提升后统一重绑定到 canonical；manifest 哈希、checkpoint ID/绑定和最终训练报告随之重算。新增回归直接证明提升后的产物满足下一轮绑定门。
- 一次性 canonical 重建保持 200 accepted / 0 rejected、142 / 5 / 53 和 4367 pairs 不变；6 个 manifest 数据摘要逐一验证，修复证据 8 / 8 文件远端与本机哈希一致。
- `train.server.20260905.r33.plan09` 随后直接通过 prepare，并以并发 1 完成 3 次迭代：同一高层队列的邮件与两个装箱候选形成 3 条 fresh/success 轨迹，睡眠边界不调用策略模型、不写控制面轨迹。
- 原生存档从 Summer 3 推进到 Summer 4，SHA-256 从 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b` 变为 `84872422a42380d669856693052cf58606273b58aed20566a9732c9751c69493`；日志明确记录一次无异常原生保存。
- canonical 数据变为 accepted 203 / rejected 0、train / validation / test = 145 / 5 / 53、train pairs = 4547；checkpoint / manifest SHA-256 为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`。
- 完整归档位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r33-round09-20260905-015829`，远端/本机 142 / 142，三类差异均为 0。Core `2274/2274`、Backend `171/171`、Release 0 warnings / 0 errors；正式训练进程已停止。
- 当时后续计划是从 Summer 4 与当前 canonical 哈希开始连续有界批次，并观察队列失效、长期回报、JSONL 恢复和累计 I/O；在跨季、跨年、Grandpa 21 与 Companion 适配验收前仍不得宣称全量训练完成。

## 历史记录 · 2026-09-21 当时权威检查点：目标日期路线组合与原子 reservation

- 新增 `acquisition_route_portfolio_proposal.v1` / `acquisition_route_portfolio_admission.v1`。调用者必须明确本批覆盖的 requirement group 和精确路线 occurrence；上游逐组执行 `all_required` 或 `choose_at_least_required_slots`，每个已选 alternative 只允许一条路线，且所有路线必须仍位于完整目标日期 Pareto 前沿。
- 组合成本继续保留非标量维度：时间、体力、精确物品/品质/实时售价与各原生货币分别聚合，不把不可比较路线强行压成单一分数，也不把调用者提案冒充独立 Teacher 偏好。
- Backend 新增唯一原子入口 `POST /api/v1/strategy/commitments/reservation-portfolios/commit`。显式释放项及全部 material/currency claim 先在内存 staging ledger 中校验；任一后项失败即整组丢弃，成功时在 repository 锁内只保存一次、ledger 只前进一个 revision，全部明细和 portfolio marker 使用同一 revision。
- Backend 全量回归 198/198；76 路线 Bootstrap 回归通过真实商店路线的材料 + 金钱联合 claim，并确认错误 requirement scope 在上游被拒绝。完整解决方案仍受本机 `f:/steam/.../Stardew Valley` 缺少 SMAPI 的既有环境门槛阻塞，非本提交编译错误。
- 正式训练仍关闭。下一固定切片是 post-commit receipt：重算提交前 admission，核验提交后 active claim 精确集合与 `base revision + 1`，再把该已提交 revision 强制绑定进每条 route execution binding；未完成前不得把“可原子提交”描述成“可正式 rollout”。

## 历史记录 · 2026-09-22 当时权威检查点：首个路线结算滚动检查点

- `acquisition_route_portfolio_settlement_receipt.v1` 已闭合完成路线的原子 reservation 结算：从已验证执行绑定和 fresh terminal receipt 派生精确请求，核对所有 completed 行、历史、route marker、单 revision 前进和带 marker 时间戳的精确重放。结算回执新增 `after_state_hash`，后续控制层不必从非权威旁路猜测终态。
- 新增 `acquisition_route_portfolio_rollout_checkpoint.v1` 与 `build-acquisition-route-portfolio-rollout-checkpoint`。首个检查点会重建当前 Teacher 选择与路线结算，把已完成 occurrence 映射回权威 requirement alternative，并按 `all_required` / `choose_at_least_required_slots` 计算已完成 alternative 和剩余槽位。
- 只有全部 scope 已满足、当前 Teacher 选中的路线全部完成、且这些路线没有遗留 active reservation 时，检查点才输出 `verified_initial_portfolio_completion`。单路线商店夹具已证明该路径；两项 `all_required` 只完成一项的纯回归会保持 `remaining_required_slots=1`。
- 未完成或多路线组合不会沿用旧 route ID，而是输出 `fresh_replan_required=true`。首检查点本身不授权下一路线；下述 continuation 边界已经闭合“累计完成 alternative -> 最新 state/ledger 剩余 Teacher 分母 -> 提交回执 -> 下一路线派发 -> fresh 终态 -> 结算 -> 下一检查点”。线性 proof manifest 可按同一实现重复任意轮次；源 proof/checkpoint 始终保持 `formal_training_authorized=false`，不能自我越权。
- `acquisition_route_portfolio_continuation_teacher_request.v1` 及两个 continuation CLI 已实现。请求必须先精确重建首检查点，并把其哈希、终态、结算后 ledger 哈希/revision、累计完成 alternative 和 transition count 一并绑定；已完成组合禁止生成请求。Teacher 只在内部排除已完成 alternatives、减少剩余 required slots，外部普通 proposal 填写完成项会被 `unverified_portfolio_continuation_evidence` 拒绝。
- 续轮候选继续复用同一完整有界组合、严格 Pareto、原子 admission/preflight 和成本聚合实现，没有第二套规划器。纯回归证明 `choose_at_least` 与 `all_required` 都不再枚举已完成项，原有 76 路线聚焦回归仍通过，Release 为 0 warnings / 0 errors。
- continuation Teacher 选出的 proposal/admission 现可经对应 continuation CLI 进入既有提交、派发、fresh 回执、结算和累计检查点证明。旧首检查点参数保留兼容；新 `acquisition_route_portfolio_rollout_proof_manifest.v1` 从首 proof 开始逐项重建全部 continuation，后续各层只消费同一个已验证 latest checkpoint 核心，不存在第三套 builder。
- 回归发现并修正了无 claim 路线的旧硬伤：过去“ledger 不变”的回执没有新 portfolio commit marker，Backend 必然拒绝后续结算。现在每个 admitted portfolio 都执行一次原子提交；无 material/currency claim 时只写唯一 ownership marker、前进一个 revision，不放宽 settlement 门禁。
- 双路线离线正例已通过：商店路线先完成并结算，首检查点留下钓鱼路线；fresh state/settled ledger 只枚举未完成钓鱼 scope，marker-only 提交后执行和结算钓鱼路线，第二检查点以 `transition_count=2` 精确证明两个 scope 全部完成。Release 构建为 0 warnings / 0 errors，聚焦四收集/76 路线自测及 Core reservation portfolio 单测通过。
- `acquisition_route_portfolio_rollout_proof_receipt.v1` 已绑定 manifest 哈希、latest checkpoint 哈希、transition count、终态 state/ledger 和完成状态。除原双路线正例外，现有三路线正例会依次完成商店购买、透明成熟作物收获和钓鱼，并在每次 fresh replan 后重建完整目标日链及与当前 tick 对齐的钓鱼预测，最终以 `transition_count=3` 通过 proof/admission；篡改 checkpoint、伪造 receipt 和不完整链仍失败关闭。
- `acquisition_route_portfolio_rollout_admission_receipt.v1` 已闭合作用域限定的 rollout controller 准入。controller 会从 manifest 重算整条有序 proof chain，并要求调用方提供的 proof receipt 与重算结果逐字段一致；只有 `proof_chain_verified=true`、`portfolio_completion_verified=true` 的终态组合可获得 `teacher_training_evidence_eligible=true`。未完成链返回 typed blocker，伪造 receipt 失败关闭。
- 成熟作物路线现复用 `executor.harvest_crop` 的同一 `CropHarvestBudgetPolicy`，只在透明逐格作物、权威最低产量和单地块目标共同证明完整需求时进入每日时间预算；多地块局部路线尚未有完整证据时继续失败关闭，不用任意常数放行。
- `acquisition_route_portfolio_supervision_dataset.v1` 现把已准入终态 portfolio 转为逐 transition、哈希链接的数据行。构建器不信任调用方标签：它重新计算 proof 与 rollout admission，要求两份外部 receipt 逐字段一致，再逐段绑定 decision snapshot/ledger、完整 Teacher 候选分母与严格 Pareto 选择、实际 queue execution、fresh terminal、settlement 和 checkpoint。
- 每行显式分开 `teacher_preference`、`native_outcome`、`student_observation`。Teacher 通道保留完整候选评估、pairwise preference、选中 proposal/route/cost，且不可用候选固定为 `defer_without_negative_label`；native 通道只记录 fresh 原生执行结果；本次 Teacher rollout 没有 Student 参与，所以 Student 通道明确为 `not_observed_teacher_rollout`，不会从 Teacher 执行伪造 Student 正例。
- 只有终态 controller admission 才能生成数据；未完成链、伪造 admission、调用方篡改数据集均失败关闭。产物仅为 `teacher_training_evidence_eligible=true`、`formal_product_training_authorized=false`，不绕过 option allowlist、正式 dataset/checkpoint、Product Executor、版本锁和原生存档边界。
- 每行还从同一已验证 decision snapshot 提取 `decision_context`：game/bridge version、save/player、年月日、time、total day、tick 和不可由调用方填写的 `save_id:year:season:day` split key；日期轴、顶层时间与 FieldEnvelope 可读性不一致时失败关闭。
- `acquisition_route_portfolio_supervision_corpus_request.v1` / `...corpus_manifest.v1` 及 `build-acquisition-route-portfolio-supervision-corpus` 已实现多 rollout 治理。每个源重新执行 dataset/proof/admission 验证；相同源只在本次构建内缓存计算但仍保留输入审计，相同行精确去重，冲突 row ID 失败，cleaned/train/validation/test JSONL 按权威 save-day 哈希分割并逐文件记录 SHA-256、bytes、rows 和 split-key count。
- `acquisition_route_target_date_daily_time_energy_budget.v2` 已把单格与多格成熟作物统一到同一类型化终端路线：按权威最低产量计算精确作物格数，逐段重证 BFS/时序，并保留每一步站位、抵达、动作和完成时刻；任一地块不可达或超出来源时间窗都会失败关闭。当前正例以 2 格 Parsnip 路线验证，不再用“单格常数”代替多格移动证明。
- continuation fixture 现暴露 3 个完整 portfolio：2 个高品质 Parsnip、1 个 Green Bean、两者都做。确定性 Teacher 严格选择 Green Bean 方案并产生 2 个真实 pairwise preference；没有 learner score，也没有伪造负例。两份重复输入仍得到 `6 input / 3 accepted / 3 exact duplicate`，全部 3 行仍属于同一 save-day/validation 分区，所以 `goal_method_trainer_input_ready=false`，明确阻塞于 train/test 缺少 pairwise 比较，而不是全局没有 Teacher 偏好。

## 历史记录 · 2026-09-22 Issue #128：source-resolved 随机结果失败关闭

- `source_resolved_downstream` 现在只表示静态来源身份已解析，不再无条件映射为零随机重试。只有 source-specific 证据证明唯一确定目标产物时才允许 `resolved_stochastic_retry_not_required`；随机来源若既未物化也没有精确概率及扩量预留重校验证据，会阻塞在 stochastic retry 轴，正式训练授权继续为 false。
- 锁定的 1.6.15 `Crop` 反编译结果表明，原版季节野生种子 `495..498` 在播种时把随机选择写入 `replaceWithObjectOnFullGrown`，成熟日再将该已选结果物化。透明桥现在读取并校验这个实时字段，而不是误把 `indexOfHarvest` 的 Data/Crops 代表值当最终产物；耕作容量和逐格作物状态共用同一个解析器，避免形成两套身份规则。
- 处理提前量轴对 `stochastic_outcome=true` 的作物只接受 `exact_from_live_native_wild_seed_replacement`。聚焦回归同时固定三种语义：普通单一产物作物可零重试、未解析的野生种子目标必须要求随机证据、fresh 状态已经确定目标结果时无需再次随机。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，完整 Stage 1 collection self-test 及未完成链/伪造 admission/篡改 dataset/corpus 源负例全部通过。本切片未启动游戏，也未写正式训练数据。下一固定切片是构造彼此独立的 train/validation/test save-day，并让每个分区都具有真实 pairwise 证据；之后实现专用 goal-to-method trainer。旧 `StructuredPolicyTrainer.BuildPairs` 仍依赖 `candidate.Selected`，不得消费本 corpus；19/19 Teacher 覆盖仍是正式全量训练的独立硬门。

## 历史记录 · 2026-09-22 独立 save-day Teacher corpus 跨过训练器输入门

- 同一条三 transition rollout 验证链现在分别从 `fixture-save-0`、`fixture-save`、`fixture-save-2` 三个独立存档身份重新生成。每份来源都独立经历完整 Teacher 分母、严格 Pareto 偏好、原子 reservation、执行回执、fresh 终态、结算、continuation 重规划、终态 proof 和 controller admission；没有复制数据集或改写 split 标签。
- 钓鱼预测刷新器随之修正一个真实身份漏绑：派生预测快照现在从基准快照同步 save/player FieldEnvelope、state identity 以及 year/season/day/total_days/time，再重新计算 state hash。旧实现只同步 tick/time/total_days，会让其他存档的预测证据混入当前 rollout；该情况现在由既有身份门失败关闭。
- 三个不可由调用方填写的 split key 分别确定落入 train、validation、test。正向 corpus 得到 `3 source / 3 unique / 9 accepted / 0 duplicate / 6 pairwise`，每个分区都是 `3 rows / 1 split key`，manifest 状态为 `ready_goal_method_trainer_input` 且 blocker 为空。单一 validation 来源重复输入的负例仍得到 `6 input / 3 accepted / 3 duplicate`，并继续因 train/test pairwise 为空而阻塞；篡改 source 仍被拒绝。
- `goal_method_trainer_input_ready=true` 只证明这批类型化 Teacher corpus 满足专用训练器的最小分区与比较信号门，不等于正式产品训练。`formal_product_training_authorized=false` 保持不变，19/19 Teacher 覆盖仍是独立硬门。本切片没有启动游戏或训练；下一固定切片是实现只消费该 corpus schema 的 goal-to-method pairwise trainer，禁止复用依赖 `candidate.Selected` 的旧 `StructuredPolicyTrainer.BuildPairs`。

## 历史记录 · 2026-09-22 专用 goal-to-method 成对训练器

- 新增 `train-acquisition-route-goal-method` 和 `explicit_teacher_pairwise_portfolio_ranker.v1`。训练器只从每行的 `teacher_preference.pairwise_preferences` 构造正负差分，明确禁止把 `candidate.Selected`、learner score、proposal/save 身份或 `dominated_by` 当作标签或特征；没有复用旧 `StructuredPolicyTrainer.BuildPairs`。
- 训练入口不会只信 corpus manifest。它逐源重读 proof manifest、proof receipt、rollout admission 和 supervision dataset，重新执行来源证明，核对每个文件摘要、cleaned/partition 摘要、split key、行数、显式 pair 引用及严格成本支配；阻塞 corpus、伪造源摘要和伪造标签声明均失败关闭。
- 检查点绑定 corpus 哈希、cleaned/train/validation/test 哈希、game/bridge/schema 版本、超参数和可选初始化检查点。`checkpoint_id` 可由这些输入重算，单独改写 ID 也会被拒绝；原子落盘不留下半成品。
- 当前独立三存档夹具产出 27 个无身份泄漏特征；train/validation/test 各为 `3 rows / 2 explicit pairs / 1.0 pair accuracy`，检查点为 `goal-method-0bde5f09208dcdea25c31bc9`。这些数字仅证明专用训练链、分区和标签边界能工作，不是泛化能力结论。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`、完整 Stage 1 collection self-test、Core game-free `81/81`、Backend `203/203`。本切片没有启动游戏，也没有写入正式训练数据。
- `formal_product_training_authorized=false` 继续保持。运行时方法排序尚未消费该检查点，19/19 Teacher 覆盖门也尚未解除。下一固定切片是加入只读的检查点推理/评分边界，并在不绕过确定性候选准入、reservation 和原生执行回执的前提下接入 goal-to-method 选择；随后扩充权威覆盖语料，而不是用当时 9 行直接启动正式产品训练。

## 历史记录 · 2026-09-22 goal-to-method 只读影子评分

- 新增 `score-acquisition-route-goal-method-row` 与 `acquisition_route_goal_method_scoring_result.v1`。评分前会重新验证 checkpoint 绑定的完整 corpus、来源 proof/admission/dataset、分区摘要和目标 row；任意换 corpus、换版本或换 row 身份都不能借用旧检查点。
- 评分器只遍历该权威 row 中 `admission_ready=true` 且具有完整非标量成本向量的候选，不接收调用方候选列表。离线训练和只读推理共用同一个特征上下文与编码器，避免字段含义分叉。
- 三候选比较样本中，模型第一名与 Teacher 选择一致，Teacher 候选 rank 为 1。输出同时保留全排名、Pareto 前沿标记和一致性结论，但固定 `selection_mode=read_only_teacher_comparison`、`portfolio_commit_authorized=false`、`formal_product_training_authorized=false`。
- 验证通过：独立评分 CLI、GoalConditionedBootstrap Release `0 warning / 0 error`、完整 Stage 1 collection self-test、Core game-free `81/81`、Backend `203/203`。未启动游戏或正式训练。
- 这一步证明检查点能被确定性重放和推理，不是实时策略接管。下一固定切片是对当时 fresh snapshot 重新构造完整候选分母，在唯一严格 Pareto 时继续服从 Teacher，在不可比前沿时只生成受门控的模型影子选择；19/19 覆盖门解除前仍不得接入 commit/dispatch。

## 历史记录 · 2026-10-12 Full Shipment 原生商店购买路线断点

- 商店购买代表已锁定为 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。它复用唯一的 `economy.buy_supplies` 路线、共享移动、`executor.interact`、可选 `executor.choose_dialogue_response`、`executor.buy_shop_item` 和菜单清理链；没有新增平行商店执行器。
- 编译器现在在同一计划内顺序投影 `move_to_tile` 后的地点与格点，使紧随其后的相邻交互按计划前态校验；投影仅在当前、目标和投影地点精确一致时生效，跨图或身份不一致仍回退到 fresh snapshot 并失败关闭。商店 supporting transition 只额外允许精确的 `move -> interact` 和 `move -> interact -> dialogue` 队列形状，乱序或扩展形状继续拒绝。
- 运行时等待会在 connector 后保存 `stable-replan-snapshot.json`，要求地点、兼容性和路线时序同时稳定。ShopMenu 已打开且 `shop_stock` 可读、非只读时，允许零游戏分钟的关菜单恢复证明，不把菜单暂停误判为路线时序失效。RuntimeTestHarness 的空闲冻结仅在 `Game1.player.CanMove` 时介入，避免地图切换淡入阶段被永久冻结。
- v3 隔离运行原生通过 Farm 到 BusStop、BusStop 到 Town，并到达 Blacksmith 稳定站位；旧 stage 4 因队列内位置未投影而拒绝。修复后对该完全相同失败产物离线重编译得到 `ready_for_supporting_transition_dispatch / dispatch_ready=true`。v4 从 fresh 隔离存档再次通过前两段，随后按用户要求主动中止；它不是失败证据，也尚不能登记为完整购买成功。
- 收尾验证：GoalConditionedBootstrap Release `0 warning / 0 error`，聚焦测试 `45/45`，完整 Core game-free `284/284`，`self-test-acquisition-route-dispatch` 通过，`git diff --check` 通过。游戏、Backend、ProductHost 和 `8765/8767/8768/8798` 监听均已退出；F 盘 v3/v4 运行证据保留，`local-data/` 未纳入版本控制。
- `formal_product_training_authorized=false` 保持不变。下一固定步骤只做一件事：从 fresh 隔离存档完整重跑 `shop_purchase_sample`，验证 Blacksmith 柜台交互、ShopMenu fresh 重规划、精确购买 `(O)380 x1`、菜单关闭及原生回执。通过前不得把本切片记为完整运行时 strata，也不得开始正式训练。

## 2026-10-27 v34 天气许可与语义路线振荡修复

- `golden-walnut-source-schedule-v34-20261027-0041` 从 day-zero 运行到 `crop_teacher_run_complete`，累计 `221` 条原生 verified action、`15` 个原生日边界且 `stage=error=0`；随后在 Spring 16 `18:10` 晴天因 `(O)143` Catfish 的 Town/Forest 路线交替停止推进。
- 根因不是 Agent 文本层：上游 Master Angler 目标日期意图没有把鱼种窗口的 `weather_modes` 与透明地点上下文天气相交。终端捕获正确拒绝晴天鲶鱼，但 supporting route 仍被反复发布。
- `MasterAnglerTargetDateIntentBuilder` 与 `MasterAnglerWindowIntentValidator` 现在共用同一地点上下文天气语义，意图生成前排除不可能窗口，运行时再次失败关闭。对 v34 真实快照重算仍有 `15` 个合法钓鱼意图，Catfish 为 `0`。
- LiveTrainingLoop 新增通用语义路线振荡守卫：同一 objective/target 在游戏时钟不前进时出现 `A -> B -> A` 反向转移即结构化停止，触发动作不进入训练数据。它只做最后防线，不把下游熔断代替上游候选许可。
- 聚焦回归 `37/37` 与完整 Stage One collection self-test 已通过；全量 Core 为 `2991 passed / 21` 个既有基线失败，隔离全解决方案 Release 构建为 `0 warning / 0 error`。v34 振荡前缀可保留为运行诊断，尾部不得形成训练监督；正式训练与全局 coverage 分子不变。下一固定步骤是提交，再从新的隔离 day-zero 存档执行 post-fix 长跑。

## 2026-10-27 v35 喷泉终止规则与钓鱼候选修复

- v35 新档长跑真实跨过 v34：累计 `196` 条 verified action、`16` 个原生日边界、`0` 条执行错误和 `0` 次语义路线振荡；晴天 Catfish 未再出现，并取得 Beach `(O)708` 与 Town `(O)145` 的合法原生捕获。作物 Teacher、任务/鱼竿、容量恢复和第二轮 8 格收获前缀均继续成立。
- 新断点来自 Town Fountain。真实透明快照显示浮标格 `(25,26)` 同时静态匹配太阳鱼，但 precedence `-10` 的 Fountain 资源规则以概率 `1` 在 precedence `0` 的太阳鱼规则之前终止；运行连续得到 Wood/Stone/Decorative Trash Can。旧候选构建器丢失了规则顺序与 fallthrough，把永远不可达的太阳鱼输出错误加入该抛竿点。
- 修复只改唯一 `FishingEventCandidateBuilder`：当更低 precedence 的适用规则被透明概率字段证明必然终止时，后续规则不再贡献该抛竿点，base fallback 也不再重复加入；同 precedence 规则和概率未完全解析的规则保持保守可达。没有新增第二套钓鱼执行器、特殊 Town 坐标表或运行时黑名单。
- 钓鱼聚焦回归 `23/23`、Master Angler `44/44`、完整 Stage One collection self-test 通过；全 Core 为 `2993 passed / 21` 个既有基线失败，失败集合未增加。v35 为人工诊断中止，没有 summary/source schedule，不能提升正式训练分子。下一固定步骤是全新 v36 原生长跑，先证明太阳鱼选择非 Fountain 抛竿点并 fresh 捕获，再继续首次登岛与 Golden Walnut source schedule。

## 2026-10-27 v36 节日等待核对与低空间恢复门

- v36 从 day-zero 新档取得 `107` 条持久化 verified action、至少 `12` 条原生日界且零执行错误。Spring 13 的 Beach -> Farm 控制项不是无效移动：Egg Festival 使 Beach -> Town 普通 warp 在 `14:10` 前关闭，因此唯一编译队列是每轮有界 `executor.wait_ticks=600`，游戏时间由 `08:40` 正常推进到 `12:30`，地点保持 Beach `(8,16)` 符合合同。
- 控制器在误判该等待为停滞后人工中止了 v36；因此这条运行只保留为诊断前缀，不声明 `(O)145` 非 Fountain 捕获或 source schedule。后续诊断必须先读取 effective queue 的 `option_id` 与 `route_wait_reason`，不能仅凭位置未变判定移动失败。
- 使用 v35 Spring 17 存档和既有 transition ledger 的恢复入口已经真实启动。它正确保留 `4` 条耐久日界、丢弃 `8` 条未被存档状态支持的 volatile harvest receipt，并执行回农场与 `crop_teacher_harvest_inventory_capacity_recovery` 两个 verified 动作，证明恢复机制本身可用。
- 恢复轮在第 3 次尝试前因 E 盘 `8,576,143,360 < 8,589,934,592 bytes` 触发 `minimum_free_space_not_met`，没有越门继续写数据。下一固定步骤是先按审核清单释放或迁移足够 E 盘空间，再复用该原生存档和 ledger 续接到 collection Teacher；不得降低 `8192 MiB` 门、重复 day-zero 或将未完成轮次提升为 coverage/训练证据。

## 2026-10-27 v39/v40 终态保存与续跑收口

- 作物 Teacher 的 terminal harvest 不再依赖进程内临时 receipt。新 ledger 创建即持久化；Teacher 完成后保留待保存终态，关闭阶段重新采集当日/存档基线，只有真正的 `executor.sleep` 结果才能创建边界 receipt，并在同一执行轮完成日期、保存哈希和 ledger 结算。
- v39 原生取得 `8` 条收获 receipt，并在 Spring 22 -> 23 的真实睡眠后写入第 `5` 条日界；v40 续接时丢弃数为 `0`，直接越过已完成 Crop Teacher，进入 Stage One collection，完成一次 objective-safe 单件出货并再次独立跨日保存。两轮最终均由 Product Executor/fresh 快照闭合，v40 无 error。
- 当前固定起点是 v40 的 Spring 24 耐久存档和已完成 crop ledger。下一施工单位只做 collection 主线：验证 `(O)145` 使用非 Fountain 浮标格并形成 fresh 原生捕获，然后继续 Golden Walnut source schedule。不得再次派发已完成作物 Teacher；若发生即视为回退。
- 该闭环仍是正式训练前置运行证据，`formal_product_training_authorized=false`，不得把控制动作或 recovery 当作 Teacher preference 标签。

## 2026-10-27 v41/v42 压缩续跑与非 Fountain 太阳鱼收口

- v41 暴露恢复长跑仍使用普通全量 JSON 快照，虽取得 6 条 verified action，但在最终睡眠前触发 `minimum_free_space_not_met`；没有最终原生存档边界，因此只保留为诊断，不算完成。
- 产品校准包装器现对 `CaptureGoldenWalnutFreshSaveEvidence` 或 Crop Teacher resume 统一传递 `--snapshot-artifact-mode content_addressed_gzip`，并把实际模式写入 summary。fresh-save 捕获开关仍只在 Golden Walnut 模式下传递，未合并两种证据语义。源守卫与内容寻址回归共 `34/34` 通过。
- v42 从 v40 只读 ledger 恢复，确认 `discarded_volatile_harvest_receipts=0` 后直接进入 Stage One collection。运行取得 `24/24` verified action；Town 非 Fountain 浮标格原生捕获 `(O)145`，随后按 exact latest catch singleton 走既有容量恢复出货，没有专用太阳鱼执行器。
- v42 最终从 Spring 24 原生睡到 Spring 25，存档哈希发生变化，持久化 `fishCaught` 含 `(O)145`；summary 为 `status=passed`、`crop_teacher_run_completed=true`、`native_save_boundary_verified=true`、`snapshot_artifact_mode=content_addressed_gzip`、`formal_training_started=false`。
- 下一固定施工单位改为独立 Golden Walnut day-zero/fresh-save source schedule。目标是首次登岛、核桃来源增量、First Parrot/Turtle/House 三项跃迁和可重放 source schedule；不得续用 crop-resume 身份冒充 fresh-save 证据，也不得提前开启正式训练。

## 2026-10-27 v43 跨目标路线续行修复

- v43 从真正 day-zero 新档启动，已完成防风草重规划、`8` 条 harvest receipt、邮件/任务/竹鱼竿原生事件链以及 `(O)131`、`(O)145`、`(O)136` 的 fresh 捕获和出货；随机鱼与杂物未被冒充为目标结果。人工停止点为 iteration `195`、verified action `162`。
- 18:20 出现 `(O)129` Beach 与 `(O)Goby` Forest 两个目标交替抢占：每次 connector 成功后 exact candidate ID 随地点变化，旧代码把候选身份丢失误当成目标完成，清空 continuation，随后 fresh ranking 又选择另一个目标。旧振荡守卫只检测同一 objective key，无法识别跨目标 `Farm -> BusStop -> Farm`。
- 修复只改控制面，不新增动作、候选或执行器：候选租约失效时保留高层 continuation；Stage One collection 和通用 DailyPlan 都通过现有 `QueueReplanFilter` 绑定同一目标；仅当当前排名中没有任何匹配候选时才写 `continuation_released` 并抑制至次日。路线守卫按真实地点边检测同一时刻的 `A -> B -> A`，objective key 仅用于诊断。
- `RouteOscillationGuardTests` 与 queue-replan/Stage One/Master Angler/Golden Walnut 相关回归共 `138/138` 通过。v43 是诊断运行，没有最终 summary/source schedule，禁止进入正式 corpus。下一固定步骤：提交修复，使用更新后的产品二进制从隔离耐久起点受限回放，要求 `(O)129` 从 BusStop 继续 Town/Beach、不得切换 Goby；通过后继续 Golden Walnut source schedule。

## 2026-10-27 v44/v45 路线续行与原生保存边界收口

- v44 使用修复后的高层 continuation 连续执行同一 `quest:13` 路线，从 Farm 经 BusStop、Town 到 Beach，没有在 fresh connector 后被其他目标抢占；随后在原生保存边界暴露新的真实缺陷：FarmHouse `building_door` 的触发格 `(64,14)` 是结构碰撞格，旧路线修复把它误当可站立终点，失败后选择 Farm→Forest→Farm 的反向绕路。通用振荡守卫在第 14 次迭代正确停止，未把失败尾部写成成功证据。
- 恢复候选现复用唯一透明路由、连接器站位解析和通用清障候选。`building_door`、`action_warp`、`locked_door_warp`、`native_bus_transport` 使用透明 `stand_tile_x/y`；step-on warp 继续以触发格为目标。若最短连接器前段只被可清除障碍阻塞，上游直接返回 `clear_obstacle_tile`，DailyPlan 进入既有 `executor.clear_obstacle`；直接高层恢复入口只等待 fresh replan，不建立第二套清障执行器。即时反向边继续失败关闭。
- v44 的真实 Farm 快照离线重算后得到可用的 `recovery:native_save_boundary / clear_obstacle_tile`，目标保持 FarmHouse，不再选择 Forest。相应连接器修复和恢复回归 `3/3` 通过。
- v45 从隔离耐久存档重新运行：8/8 Product action 完成；同一 `quest:13` 高层目标依次执行 Farm→BusStop→Town→Beach；关闭阶段按 Beach→Town→BusStop→Farm→FarmHouse 滚动重规划，随后原生睡眠。日期从 `1:spring:14` 变为 `1:spring:15`，存档 SHA-256 从 `a9dbf56f...` 变为 `74400983...`，summary 为 `status=passed`、`native_save_boundary_verified=true`、`formal_training_started=false`。
- v45 证明通用跨图 continuation 与原生保存边界闭合，但没有单独重现 `(O)129`；不得把 quest 路线证据改写成该鱼的完成证据。下一固定施工单位仍是独立 Golden Walnut day-zero/fresh-save source schedule，并把 `(O)129` 跨 BusStop/Town 连续性作为长跑观测点，随后证明首次登岛、核桃来源增量及 First Parrot/Turtle/House 三项跃迁。

## 2026-10-27 v46/v47 商店候选前置日程门

- v46 在 Spring 1 周一错误锁定 AnimalShop 购买目标并做了 14 个机械等待块；`08:20` 的 fresh snapshot 才正确指出玛妮不在柜台。复核完整透明日程后确认，玛妮当天没有任何位置落入柜台服务区且与入口开放时间重叠，因此错误发生在 `06:00` 候选生成，而不是店主状态读取或购买执行器。
- 修复复用 `NpcCurrentScheduleSnapshotProjectionResolver`、`NpcCurrentLoadedSchedulePresenceResolver` 与 `NpcFuturePresenceWindowResolver`。购买候选只接受店主稳定位置窗口、服务区矩形、店铺时间和入口时间的真实交集；无交集当天排除，有未来交集精确延迟，证据不完整则失败关闭。`SnapshotEnvelope` 原生入口避免序列化整份全量快照，单快照同店主解析结果被缓存。
- `v47` 首轮真实 ranking 中 AnimalShop 候选全部以 `shop_owner_has_no_service_window_today` 排除，随后直接编译 `claim_starter_gift`，没有重现 v46 的购买等待。运行在该诊断点停止，未宣称 Golden Walnut source schedule 完成。
- 下一步：从独立新档继续 source-schedule 长跑；若出现新断点，只修该 fresh 快照证明的候选、编译或执行缺口，保持首次登岛 -> 核桃来源增量 -> First Parrot/Turtle/House -> source schedule 生成/重放的顺序。

## 2026-10-27 v48 双作物原生循环与节日等待优化

- v48 新档真实完成两轮 Crop Teacher：`6` 格花椰菜和 `8` 格防风草都走过购买/库存取种、场地准备、播种、逐日浇水、补水、跨日保存与全量收获。第二轮满包时复用既有安全单件出货恢复两次，最终 `8/8` 收获回执齐全。人工停止前共有 `185` 条 verified action，Stage One 已重新接管。
- 邮件、任务 `13`、Beach 路线、节日门禁和 shared daily plan 之间的控制交接都真实发生；节日关闭没有卡死，`14:10` 后自动改编译连接器。但每个 `600` tick 等待片段都触发 full snapshot、候选生成与排名，属于已证实的性能缺口。
- 修复只扩展既有 `executor.wait_ticks` 合同：`wait_ticks` 继续限制为 `1..600`；只有编译器给出精确 `route_wait_until_time` 时，Runtime Harness 才允许最多 `3` 个连续原生片段，开放即提前返回，段末仍由 Product Executor/fresh full snapshot 闭合。普通等待保持单片，clock/full 哈希不交叉复用，时钟无进展失败关闭。
- 有界等待 primitive 的隐藏原生 Harness smoke 已通过：`06:00 -> 06:20`、`actual_ticks=875`、`applied + verified`，证明同一原生请求能越过原 `600` tick 单片并在目标时刻提前停止；它未绕过执行器写游戏状态。全量 Core 为 `3004 passed / 21` 个既有基线失败，没有新增失败。下一步是提交本切片并继续 Golden Walnut source schedule，在真实 Product Executor/节日门再次观察路线级集成。v48 及本优化均不等于首次登岛或正式训练准入。

## 2026-10-27 v49-v52 crop-resume collection 闭环

- v49 以 v48 的 Spring 17 存档和只读 transition ledger 恢复，按耐久边界丢弃 `8` 条尚未跨日落盘的收获回执，重新完成两次容量恢复与 `8/8` 防风草收获；原生睡眠后得到 Spring 18 存档和 `8 harvest + 5 day boundaries` 派生账本。该轮 `5` 条 verified action、`2` 条已接纳多候选轨迹，包装器通过。
- v50 从耐久账本直接越过 Crop Teacher，执行 `(O)330` 安全单件出货和 Master Angler 路线，取得 `4` 条 verified action 并保存到 Spring 19。v51 随后实际捕获 `(O)147`，但因有界尝试同时计入 fresh 失效/continuation 释放而停在 `6/8`，未写原生保存边界；该轮只作诊断。
- v52 以 `6/12` 的可达门重跑未落盘的 Spring 19 状态，原生捕获目标 `(O)131`，产生 `1` 条 `fishing.catch_fish` 已接纳轨迹；满包恢复通过唯一跨图 continuation 将该鱼精确投箱，库存 `1 -> 0`、出货箱 `0 -> 1`。原生睡眠把存档推进至 Spring 20，summary 为 passed，所有服务和端口正常回收。
- 下一固定施工单位仍是独立 Golden Walnut day-zero/fresh-save source schedule：首次登岛 -> 来源增量 -> First Parrot -> Turtle -> House -> schedule 生成与重放。v49-v52 不得用于提升 fresh-save coverage 或开启正式训练。当前 E 盘只保留约 `8.1 GiB` 可用空间，继续长跑前必须恢复高于 `8192 MiB` 门槛的实际写入余量；不得关闭或降低该门。

## 2026-10-27 v53 fresh-save 诊断收口

- v53 新档真实取得 `193` 条 verified Product action、两轮完整 Crop Teacher 和 total day 16 的耐久前缀，但在 `19:20` 的第三次作物 handoff 中，普通 `route_to_farm` 与共享 exclusive recovery 门冲突，连续八次被 dispatch 拒绝后停止。该运行是高价值诊断，不是完成的 Golden Walnut source schedule。
- Crop Teacher 现于 `19:00` 后复用既有 `recovery.stabilize_day`，不再重建普通作物路线；执行步骤覆写保留原 ledger 的 `NativeSleep` 日界结算语义。故事事件和安全对话恢复仍先于该门，其他普通 interrupt 仍不得结算作物或睡眠转移。
- fresh-save continuity 从整块富投影文本相等改为稳定进度投影相等，并把控制器交接窗口锁为 v53 实测的 `24` tick。当前位置、支付能力和当前地图校验状态可变化；任一实际核桃、来源收集、修船、到岛、鹦鹉工程或访问解锁变化若没有 episode 仍被拒绝。v53 的 `193/193` episode 已离线重建 manifest 成功。
- 验证：聚焦 `22/22 + 11/11`；全量 Core `3008 passed / 21` 个既有基线失败。下一步只运行新的独立 day-zero 长跑，先观察晚间 recovery 原生保存，再继续首次登岛与核桃 schedule；本轮没有写正式训练数据，`formal_product_training_authorized=false`。

## 2026-10-29 EVD-343 钓鱼奖励菜单恢复闭环

- 长跑中满包钓鱼后的 essential FishingRod `ItemGrabMenu` 不再落入普通关菜单或停滞。透明投影、强制恢复候选、DailyPlan、编译、类型化请求、原生槽位点击和 fresh 回执已贯通；无空位时只允许一项透明、可丢弃、未保护且总店售价值最低的 Object 栈，缺少安全项即失败关闭。
- E 盘隐藏静音原生矩阵 `runtime-fishing-item-grab-recovery-20261029-064618` 为 `2/2 PASS`：直接领取与授权单项丢弃后领取均 `applied/verified`，奖励库存精确 `0 -> 1`，菜单关闭。诊断 fixture 与生产执行器继续分离。
- 为避免恢复动作触发全地图扫描，新增 `fishing_reward_recovery` 目的配置。复合选项只在精确菜单身份下允许缺少与该分支无关的三个路线字段；其他恢复动作仍按原 `recovery` 配置失败关闭。最终性能为 39.381 UPS、末次构建 40.673 ms、启动期最大 559.851 ms。
- EVD-343 登记为底层 executor 校准证据，TrainingAllowlist 不变，`formal_product_training_authorized=false`。下一施工单位回到 Golden Walnut fresh-save source schedule，而不是继续扩展此恢复动作。

## 2026-10-29 v61 长跑断点与 v62 固定入口

- v61 从独立 day-zero 新档推进到 Spring 23 / total day 22，共 `395` 次迭代、`295` 条 verified Product action。它真实通过 EVD-343 钓鱼奖励恢复，但仍未首次登岛，未生成 Golden Walnut source schedule。
- 第一个断点是 episode 297 的优先剧情中断：`executor.advance_story_event` 产品回执实际为 `applied + verified`，但旧聚合回执把“当前作物候选尚未完成”误写成“本次执行 blocked”，导致持久化 episode 从 296 跳到 298。PR #349 现将“已执行步骤全部验证且快照新鲜”和“整个候选完成”分开；前者保留 executor-calibration episode，后者仍为 false，策略轨迹过滤器继续拒绝不完整候选。`24` tick 连续性上限没有放宽。
- 第二个断点发生在睡醒后的短暂原生控制交接：校准与 native gate 已开放、菜单/事件/工具/warp 均关闭，但 `CanMove=false` 且 route timing 暂不可用。PR #348 只在这一精确状态编译一次 `30` tick 等待并 fresh 重规划；该等待进入 no-progress 退避，不计有效训练动作。
- v61 已存在不可补写的 episode 缺口，只保留为诊断，不得重建为正式 corpus。下一固定入口是全新 v62 day-zero 隔离长跑；退出条件依次为：复现并通过剧情中断连续性、通过 native-control settle、保持零未验证执行，随后继续首次登岛 -> 核桃来源增量 -> First Parrot -> Turtle -> House -> source schedule 生成与独立重放。#345 仍生效，运行必须使用 `--skip-training`，`formal_product_training_authorized=false`。
- v62 启动前的夜间降载修正已闭合：产品校准脚本现在显式设置并恢复 `STARDEWAI_SUPPRESS_LOCAL_RENDER=1`，RuntimeTestHarness 的既有 Draw/SaveGameMenu 补丁同时覆盖 headless training，而不影响普通可见游戏。短原生 smoke `headless-training-render-suppression-20261029` 为 passed，日志确认本地 Draw 被抑制、stderr 为零、原生新档提交成功且全部进程/端口回收；该 smoke 不构成 source schedule 或训练证据。

## 2026-10-29 v63-v65 节日长等待闭环

- v63 从独立 day-zero 新档运行 `208` 次迭代并取得 `155` 条 verified action，推进到 Spring 13 / total day 12。Stage One collection 在 Egg Festival 普通 Town transit 于 `14:10` 前关闭时正确让权，但共享日计划把所有 `AllowedToday=true / NextOpenTime=1410` 候选过滤为空；连续八轮得到 `empty_action_list` 后结构化停止。
- 根因是 `EventCandidateTimelineScheduler` 的默认等待预算固定为 `21600` ticks（6 游戏小时），而 `06:40 -> 14:10` 需要 `27000` ticks。PR #352 把**默认**预算改为当前时刻到 `19:00` autonomous recovery 窗口的剩余时间；显式调用者给出的硬上限仍原样生效。ready-now 候选仍排在 deferred 之前，DailyPlan 仍只编译单个 `600` tick 滚动等待并 fresh 重规划，因此不会整天盲等或抢占可立即执行的工作。节日路径聚焦回归 `8/8`、GameFreeGovernance Core `503/503` 通过。
- v64 尝试从 v63 的白天存档直接续跑时，被 day-zero initial-boundary validator 正确拒绝。跨进程重载只能回到最近一次原生保存边界，不能把 Spring 13 内存态拼接成连续 fresh-save corpus；该尝试没有接纳 episode，相关进程和端口已完整回收。
- v65 随后从新的 day-zero 隔离存档启动，并复用已验证的预构建二进制降低启动开销。截至 iteration `117`，耐久链已推进到 Spring 13 `15:00`、三类 stderr 均为零；iteration `112-113` 为单步 `executor.wait_ticks`，`14:10` 开放后 iteration `114-116` 自动切换为 `executor.traverse_connector`，iteration `117` 继续移动/显式清障。该结果真实关闭 v63 的长等待阻塞，而不是只证明单元测试。
- v65 最终运行 `356` 次 primary attempt，留下 `265` 条耐久 episode，并推进到 Spring 22 / total day 21；作物 Teacher 的首日、整轮、一次重规划、`8` 条收获回执和 `4` 条日界回执均完成。运行在 episode `279` 的 verified `executor.advance_story_event` 与 episode `281` 的 `executor.water_crop` 之间暴露 `335` tick 控制器暂停交接。两侧保持同日 `06:00`、Farm `(64,15)`、`403g`、`270` energy、`100` health、Watering Can 和逐项相同背包，岛屿/核桃进度也完全相同；这不是未记账游戏进展。
- 连续性验证器现只为上述已验证剧情动作增加最大 `360` tick 的窄交接：前一动作必须是成功且 primitive verified 的 `executor.advance_story_event`，下一条必须是成功 executor episode，并且存档/玩家、日与分钟、地点/格、金钱、能量、生命、工具、背包、姜岛与核桃进度全部相同。普通交接仍严格限制为 `24` tick，任何玩家移动、背包变化、岛屿进度或超限都失败关闭。聚焦边界测试 `5/5`、GameFreeGovernance Core `508/508` 通过。
- 使用修复后二进制对 v65 耐久目录重建，`265/265` episode 已形成合法 `golden_walnut_fresh_save_episode_manifest.v2`。随后 source-schedule builder 精确失败为 `Fresh-save episodes do not contain the native first-island-arrival transition.`，证明证据基础设施阻塞已关闭，真实缺口回到首次登岛。v65 已停止且不能跨进程拼接未保存内存态；下一运行必须是新的 day-zero v66，顺序仍为首次登岛 -> 来源增量 -> First Parrot -> Turtle -> House -> source schedule 生成与独立重放。#345 完成前继续 `--skip-training`，`formal_product_training_authorized=false`。

## 2026-10-30 v69 部分成功聚合的严格证据闭环

- v69 从新 day-zero 存档运行到 Spring 22，累计 `327` 次迭代、`263` 个 verified Product action。iteration `316` 的八项收获中一项成功、七项因容量 blocked，已真实产生八个 continuity-only 耐久 alias，不写政策数据；旧 early-return 证据空洞因此关闭。
- 离线 builder 已从停止后的耐久目录生成 `golden_walnut_fresh_save_episode_manifest.v3`，严格接纳 `494` 个 episode，包含上述八个别名。同一 manifest 构建 source schedule 时只因缺少原生首次登岛 transition 而失败，不再是连续性或快照错误。
- 停止点在第三轮 `8/8` 收获完成后，但未再跨原生睡眠/保存边界。下一唯一施工单元是从最近耐久保存恢复的有界回放：重结算必要的 volatile 收获，先完成原生保存，再继续首次登岛与核桃 source schedule。仍使用 `--skip-training`，不改变任何正式训练准入分子。

## 2026-10-30 v70 校准结果与下一施工单元

- v69 的未保存尾部不能跨进程升级为 fresh-save corpus，因此 v70 直接采用新的 day-zero 身份；这是对上一节“先有界回放”表述的修正。v70 运行到 Spring 17 / iteration `189`，取得 `173` 条 verified Product action、两轮完整作物收获、节日 transit 和 Stage One 钓鱼前缀，但仍没有首次登岛。
- 真实排名工件证明容量恢复把 Full Shipment contribution 错排在腾格效率之前：`20` 木材压过两个 singleton，形成 20 次原生单件出货。PR #360 只调整同一 `StageOneInventoryCapacityRecovery` 的候选顺序，优先最少 native deposit 次数；候选、编译器、产品执行器和回执均继续复用现有单一路径。
- v70 在缺陷归因后停止，未保存尾部不续接。下一施工单元为全新 v71：用合并后代码复核 singleton 恢复，然后继续首次登岛和 Golden Walnut source schedule。运行仍为 `--skip-training` 的校准采证，不是模型训练。

## 2026-10-30 v71 校准结果与下一施工单元

- v71 仍为 `--skip-training` 校准。iteration `149` 已在真实满包场景选择可用 singleton `(O)92 x1`，通过既有单件出货 primitive 释放槽位；iteration `150` 完成 `8/8` 收获。容量恢复的最短动作排序已从单元证明提升为原生运行证明。
- iteration `162` 的原生钓鱼在满包时打开 essential `ItemGrabMenu`。iteration `165` 的统一排名中已有可执行的 mandatory `recovery_claim_fishing_item_grab_reward`，但旧 Stage One membership 只接纳四类 collection 目标，因而先错误让出控制；后续 Crop Teacher 路线被仍开启的菜单正确拒绝。该断点是控制编排顺序缺口，不是缺候选、缺字段或缺执行器。
- 当前修复让已存在的 mandatory menu recovery 在 continuation、collection membership 与策略选择之前执行，并继续复用唯一 DailyPlanCompiler、ActionQueueCompiler 和 Product Executor。它只形成 control queue，不生成 Teacher preference 标签，也不解除正式训练门。验证为聚焦 `11/11`、GameFreeGovernance `521/521`、Release build 零警告零错误。
- v71 尾部不可跨未保存边界续接。下一施工单元为全新 v72 day-zero 校准：先原生证明钓鱼奖励恢复被 Stage One 接管、菜单未关闭前不派发 Crop Teacher，再继续首次登岛、核桃来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放；#345 完成前不启动模型参数训练。

## 2026-10-30 v72-v82 淡入控制交接修复与当前施工单元

- v72 在 `--skip-training` 下取得 `176` 条 verified Product action 并推进到 Spring 9，最终由五次相同的 `future_route_movement_timing_context_outside_calibration_scope` 受控停止。真实快照证明这是日界后的精确淡入交接：校准兼容、路线 timing 未就绪、native gate 未开、`player_can_move_now=true`、`global_fade=true`，其余菜单/事件/工具/暂停/冻结/warp 条件全部关闭。
- 唯一 `NativeControlSettleRecovery` 现并列接受两个精确且互斥的状态：原 post-sleep control gap，以及 v72 的 day-start fade control gap。两者都只产生一次 `30` tick 控制等待并 fresh 重规划；混合状态、缺字段或其他控制状态继续返回 null。调用点已从 Stage One collection builder 提升到所有 DailyPlan producer 之前的唯一全局抢占点，避免 active Crop Teacher 或普通日计划先行覆盖恢复。
- v73 从 v72 的 Spring 9 保存边界隔离启动，完成 `2/2` verified action 和 Spring 9 -> 10 的原生保存。v76 正确拒绝过期 fixture；v77 的 Crop Teacher 睡眠在返回前已等待淡入结束；v79/v80 的 fresh-process 共享睡眠同样未复现。v82 从 v72 Spring 8 `_old` 精确哈希副本执行 `8 x 600` tick 预热后，再通过同一 DailyPlanCompiler、ActionQueueCompiler 与 Product Executor 执行共享睡眠及次日新规划；预热全部 verified 且不进入训练数据，但仍未出现 transient。包装器缺少 settle wait/fresh replan 双标志时严格失败，因此这些运行均没有冒充正向命中。
- 当前验证为专项 `19/19`、完整 GameFreeGovernance `526/526`、Release build 零警告零错误。人工探针在 v82 收口，不再继续扩大；实时正向命中只在后续自然长跑碰到 v72 状态时补录。当前 WIP 直接恢复首次登岛、核桃增量、First Parrot、Turtle、House、source schedule 生成/重放与 Master Angler prerequisite audit。所有运行继续 `--skip-training`，#345 与 `formal_product_training_authorized=false` 保持。

## 2026-11-01 当前 WIP：矩阵锁对齐与 v97 继续运行

- 当前源码、`knowledge-artifacts.lock.json`、原生动作分母和锁定 1.6.15 full snapshot 已重新写入 `current-option-input-lock.v1.json`。唯一正式构建脚本重放成功：`585/585`、`blocking=0`、`242` options、`163` runtime verified、`67` training eligible，输出哈希为 `418a2272a18db888666a5d9842503f9198356aec0e456ec42a5ea2e4c1465c2b`。这次锁更新是对既有四项登记的追认，不是扩张训练动作集合。
- 历史 ready corpus 无法在当前 daily time/energy 规则下确定性回放，已通过现有 Stage One fixture 原样重建 train/validation/test 三分区，而没有创建并行数据管线。更新后的 9 行 corpus 与当前 requirement inventory、acquisition lowering 和 option matrix 联合通过 Goal Method Teacher coverage 自测。
- 首次完整重建在更晚的 Master Angler 断言停下。诊断证明错误蟹笼候选仍以 `master_angler_crab_pot_candidate_is_not_exact_collection_eligible_target` 被拒；真正漂移是 fixture helper 漏写 `AllowedNow=true`，使本应 ready-now 的 route 被误判 deferred。补齐该测试输入字段后，完整 `self-test-current-stage-one-collection` 通过，产品 matcher 与 strict crab-pot identity 无改动。
- 当前代码改动仅为输入锁和测试夹具；本地生成的矩阵、降级表、corpus、自测输出均不可提交。提交前固定检查：重跑 option matrix lock、完整 Stage One、Goal Method coverage、`git diff --check`，并确认 `git status` 中没有 `local-data`。
- v97 继续独占真实游戏实例，仍按启动时二进制运行，不热替换、不并发启动 v98。低频监控只看进度日志与终止状态；终止后先归档其真实证明/阻塞，再决定是否以最新提交启动下一轮。
