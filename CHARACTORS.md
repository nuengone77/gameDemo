# TASK: Implement Character Combat Skills for 5 Playable Heroes

## 1. Objective

พัฒนาและเชื่อมต่อระบบการต่อสู้และสกิลของตัวละครที่เล่นได้ทั้ง 5 ตัว ให้สามารถใช้งานได้จริงภายในเกม โดยแต่ละตัวละครประกอบด้วย

- Basic Attack — การโจมตีปกติ
- Active Skill — สกิลกดใช้งาน
- Passive Skill — สกิลติดตัว
- Ultimate Skill — ท่าไม้ตาย

ให้ตรวจสอบและทำความเข้าใจโครงสร้างโปรเจกต์ ระบบตัวละคร ระบบต่อสู้ ระบบศัตรู และระบบสกิลที่มีอยู่ก่อนเริ่มแก้ไขโค้ด

**ข้อกำหนดสำคัญ**
- ใช้ภาษา Framework และ Architecture ตามที่โปรเจกต์ใช้อยู่
- Reuse ระบบเดิมให้มากที่สุด
- ห้ามสร้างระบบ Combat, Character หรือ Skill Manager ใหม่ซ้ำซ้อนโดยไม่จำเป็น
- ไม่แก้ไขหรือทำลายฟีเจอร์เดิมที่ทำงานได้อยู่แล้ว
- แยก Logic, Animation และ Visual Effects ออกจากกันตามความเหมาะสม
- ค่าสถานะ ความเสียหาย Cooldown และ Duration ควรปรับแต่งได้ผ่าน Config
- ทุกสกิลต้องสามารถทำงานร่วมกับระบบ Collision, Damage, Buff, Debuff และ Enemy AI ของเกมได้
- หากไม่มี Asset หรือ Animation ให้ใช้ Placeholder Effect ชั่วคราวโดยไม่ทำให้ Logic หยุดทำงาน

---

# 2. Character Specifications

## CHARACTER 01 — TANKY

**ชื่อ:** แทงค์กี้ (Tanky)

**ฉายา:** อัศวินผลึกแสง

**Combat Role:** Defensive Tank / Ranged Magic / Team Protection

**แนวทางการเล่น:** แทงค์ระยะไกลที่ใช้โล่เวทมนตร์ลอยได้ 4 ชิ้นในการป้องกันทีม ควบคุมพื้นที่ และโจมตีจากแนวหลัง

### Weapon: ศิลาดาราเทวา (Astral Meteor Shields)
- โล่ผลึกเวทมนตร์จำนวน 4 ชิ้น
- ลอยหมุนวนรอบตัวละครตลอดเวลา
- ควบคุมตำแหน่งได้อย่างอิสระ
- เมื่อใช้สกิลป้องกัน โล่ทั้ง 4 จะเคลื่อนที่ไปรวมตัวกันเป็นกำแพง

### Basic Attack — Light Crystal Shot
- ยิงกระสุนผลึกแสงเวทมนตร์จากฝ่ามือ
- โจมตีระยะไกลเป็นเส้นตรง
- สร้าง Magic Damage เมื่อกระทบศัตรู
- ใช้ระบบ Projectile และ Collision ของโปรเจกต์

### Active Skill — Astral Shield Wall
- สั่งให้โล่ทั้ง 4 พุ่งไปด้านหน้าตัวละคร
- ขยายขนาดและเชื่อมต่อกันเป็นกำแพงป้องกัน
- ป้องกัน Projectile ของศัตรูที่พุ่งเข้ามาจากด้านหน้า
- มอบ Defense Buff ให้ตัวละครและพันธมิตรที่อยู่ด้านหลังกำแพง
- เมื่อหมดระยะเวลาสกิล โล่ต้องกลับเข้าสู่ตำแหน่งโคจรรอบตัว

**Implementation Requirements**
- Shield Wall ต้องมี Collision หรือ Blocking Logic ที่ใช้งานได้จริง
- ต้องตรวจสอบทิศทางและตำแหน่งของการป้องกัน
- ต้องรองรับ Projectile หลายลูกในช่วงเวลาเดียวกัน

### Passive Skill — Astral Absorption
มีผลติดตัวตลอดเวลาประกอบด้วย 2 ความสามารถ

**1. Distance Defense Scaling**
- พลังป้องกันเพิ่มขึ้นตามระยะห่างระหว่างตัวละครกับศัตรู
- โบนัสป้องกันสูงสุด 30%
- คำนวณโดยใช้ระยะห่างจากศัตรูที่เกี่ยวข้องกับการโจมตี
- ระยะสำหรับการได้รับโบนัสสูงสุดต้องกำหนดใน Config

**2. Damage-to-Shield Conversion**
- ทุกครั้งที่การโจมตีของ Tanky สร้าง Damage สำเร็จ
- นำ 10% ของ Damage ที่สร้างจริงมาเป็น Shield HP ให้ตัวเอง
- Shield HP ใช้ดูดซับความเสียหายก่อน HP ปกติ
- กำหนด Maximum Shield HP ใน Config เพื่อป้องกันการสะสมอย่างไม่จำกัด

### Ultimate Skill — Meteor Binding
- เลือกพื้นที่เป้าหมายและเรียกอุกกาบาตยักษ์ตกลงมา
- สร้าง Magic AoE Damage อย่างรุนแรง
- ศัตรูที่อยู่ในพื้นที่ได้รับ Root เป็นเวลา 2.5 วินาที
- ลด Attack Power ของศัตรูที่โดนลง 30%
- แสดง Meteor Impact VFX และพื้นที่ผลกระทบ

**Expected Behavior**
- Root หยุดการเคลื่อนที่แต่ไม่จำเป็นต้องหยุดการโจมตี
- Attack Reduction ต้องใช้ระบบ Debuff และคืนค่าปกติเมื่อหมดผล

---

## CHARACTER 02 — SHADOW

**ชื่อ:** ชาโดว์ (Shadow)

**ฉายา:** เงาสังหารไร้เสียง

**Combat Role:** Ranged Assassin / High DPS / Attack Speed Scaling

**แนวทางการเล่น:** ตัวละครเน้นความเร็วในการปาคุไน สร้างความเสียหายต่อเนื่อง และเพิ่มความเร็วโจมตีเมื่อโจมตีศัตรูอย่างต่อเนื่อง

### Weapon: Shadow Kunai
- คุไนเวทมนตร์สีดำ
- สามารถสร้าง Projectile ได้ต่อเนื่อง
- มีลักษณะการโจมตีเร็วและทะลวงเกราะ

### Basic Attack — Shadow Pierce
- ปาคุไนไปยังเป้าหมายด้วยความเร็วสูง
- สร้างความเสียหายระยะไกล
- มีความสามารถ Armor Penetration
- แสดง Trail Effect สีดำ

### Active Skill — Kunai Overdrive
- เพิ่ม Attack Speed ชั่วคราว
- เปลี่ยนรูปแบบการปาคุไนเป็นการยิงกระจายรูปพัด (Fan-Shaped Projectiles)
- ตัวละครพุ่งถอยหลังเพื่อเว้นระยะห่างจากศัตรู
- สร้าง Afterimage Effect ระหว่างเคลื่อนที่

**Implementation Requirements**
- รองรับการยิงคุไนหลายเล่มพร้อมกัน
- Projectile ต้องมีทิศทางกระจายตามมุมที่กำหนด
- การพุ่งถอยหลังต้องตรวจสอบ Collision เพื่อไม่ให้ทะลุสิ่งกีดขวาง

### Passive Skill — Endless Excitement
- เมื่อการโจมตีด้วยคุไนโดนเป้าหมายต่อเนื่อง จะสะสม Hit Stacks
- แต่ละ Stack เพิ่ม Attack Speed
- หากหยุดโจมตีโดนเป้าหมายเกินเวลาที่กำหนด ให้ Reset Stack
- กำหนด Max Stacks หรือ Attack Speed Cap ใน Config เพื่อป้องกันค่าที่เพิ่มไม่จำกัดจนทำให้เกมหรือระบบ Projectile ทำงานผิดปกติ

**Expected Behavior**
- เพิ่มความเร็วโจมตีอย่างเห็นได้ชัดเมื่อโจมตีต่อเนื่อง
- Reset Stack เมื่อขาดการโจมตีต่อเนื่อง
- ไม่สร้าง Stack จากการโจมตีที่ไม่โดนศัตรู

### Ultimate Skill — Dark Kunai Rain
- ตัวละครกระโดดหรือทะยานขึ้นกลางอากาศ
- ปล่อยออร่าเวทมนตร์สีดำ
- เรียกคุไนจำนวนมหาศาลตกใส่ศัตรูทั่วพื้นที่ที่มองเห็น
- สร้างความเสียหายวงกว้างอย่างรุนแรง
- แสดง Rain of Kunai VFX

**Performance Requirements**
- ใช้ Particle System, Object Pooling หรือวิธีที่เหมาะสมกับ Engine
- ไม่จำเป็นต้องสร้าง Projectile จริงหลายพันชิ้น
- แยก Visual Effect ออกจาก Damage Calculation
- ป้องกันการสร้าง Damage ซ้ำโดยไม่ตั้งใจ

---

## CHARACTER 03 — MAGICIAN

**ชื่อ:** หนุ่มน้อยนักมายากล (Magician)

**Combat Role:** Ranged DPS / Crowd Control / Evasion

**แนวทางการเล่น:** โจมตีศัตรูหลายตัวด้วยไพ่เด้งชิ่ง ใช้กับดักควบคุมพื้นที่ และเอาตัวรอดด้วยการหลบหลีกและ Teleport

### Weapon: Magic Card Deck
- ไพ่เวทมนตร์คมกริบ
- ใช้เป็น Projectile สำหรับโจมตีศัตรู
- สามารถสะท้อนหรือเด้งชิ่งไปยังเป้าหมายอื่นได้

### Basic Attack — Ricochet Cards
- ปาไพ่โจมตีเป้าหมาย
- เมื่อโจมตีโดนศัตรู ไพ่สามารถชิ่งไปหาเป้าหมายถัดไป
- เลือกเป้าหมายใหม่ภายใน Bounce Radius
- จำกัดจำนวน Bounce ผ่าน Config
- ป้องกันไม่ให้ไพ่ชิ่งกลับไปโจมตีศัตรูตัวเดิมซ้ำภายในชุดการโจมตีเดียวกัน

### Active Skill — Explosive Card Trap
- วางไพ่กับดักลงบนพื้น
- กับดักรอให้ศัตรูเข้ามาใน Trigger Area
- เมื่อทำงานจะระเบิดสร้าง AoE Damage
- ทำให้ศัตรูได้รับ Knock-up
- แสดง Effect ระเบิดและการลอยขึ้นของศัตรู

**Implementation Requirements**
- รองรับ Trap Lifetime
- รองรับ Trigger Detection
- Knock-up ต้องสามารถรบกวนการเคลื่อนที่หรือ Action ของศัตรูตามระบบ Crowd Control ที่เกมรองรับ

### Passive Skill — Illusion Dodge
- เพิ่มโอกาสหลบหลีกการโจมตีโดยอัตโนมัติ
- เมื่อ Dodge สำเร็จ จะไม่รับ Damage จากการโจมตีนั้น
- ใช้ Evasion Probability ที่กำหนดใน Config
- สามารถเพิ่ม Illusion หรือ Afterimage Effect เมื่อหลบสำเร็จ

### Ultimate Skill — Jackpot
- สร้างพายุไพ่เวทมนตร์หมุนวนรอบตัวละคร
- สร้าง AoE Damage ต่อศัตรูที่อยู่ในรัศมี
- หลังจากเริ่มใช้ Ultimate ให้ Teleport ตัวละครไปยังตำแหน่งปลอดภัย
- ตำแหน่ง Teleport ต้องไม่อยู่ในกำแพงหรือสิ่งกีดขวาง
- ต้องมี Logic เลือกตำแหน่งปลอดภัย
- หากไม่มีตำแหน่งปลอดภัย ให้ใช้ Fallback ที่ไม่ทำให้ตัวละครติดอยู่ใน Geometry

**Expected Behavior**
- ไพ่พายุสร้างความเสียหายตามช่วงเวลาหรือจำนวนครั้งที่กำหนด
- Teleport ไม่ควรทำให้ตัวละครออกนอกพื้นที่เล่น

---

## CHARACTER 04 — ORACLE WITCH

**ชื่อ:** แม่มดผู้หยั่งรู้ไร้พ่าย (Oracle Witch)

**Combat Role:** Magic Burst / Crowd Control / Defense Penetration

**แนวทางการเล่น:** โจมตีช้าแต่รุนแรง ควบคุมความเร็วของศัตรู และใช้เวทมนตร์ที่สามารถทะลุสิ่งกีดขวางและการป้องกันได้

### Weapon: Ancient Stone Staff
- คทาศิลาโบราณ
- ปล่อยเวทมนตร์ผ่านลูกแก้วพลังงาน

### Basic Attack — Arcane Orb
- ปล่อยลูกแก้วเวทมนตร์เคลื่อนที่ด้วยความเร็วต่ำ
- เมื่อสัมผัสเป้าหมายจะระเบิด
- สร้าง AoE Magic Damage สูง
- รองรับ Projectile Lifetime และ Explosion Radius

### Active Skill — Time Distortion Field
- สร้างพื้นที่เวทมนตร์บนพื้น
- ศัตรูภายในพื้นที่จะได้รับ Movement Speed Reduction อย่างมาก
- ใช้ได้กับศัตรูหลายประเภท เช่น Goblin และ Golem
- แสดงพื้นที่อาณาเขตและ Effect หน่วงเวลา
- เมื่อออกจากพื้นที่หรือพื้นที่หมดอายุ เอฟเฟกต์ Slow ต้องสิ้นสุดตามกฎของระบบ

### Passive Skill — Distorted Trajectory
- การโจมตีเวทมนตร์ของตัวละครสามารถทะลุผ่านสิ่งกีดขวางที่อนุญาตได้
- ไม่ถูกป้องกันโดยโล่เวทมนตร์หรือเกราะประเภทที่กำหนด
- รองรับ Armor Bypass / Defense Bypass ตามระบบเกม
- ต้องแยกการทะลุสิ่งกีดขวางออกจากการทะลุเกราะป้องกัน เพราะเป็นคนละกลไก

**Implementation Requirements**
- ไม่ควรให้การโจมตีทะลุขอบเขตแผนที่หรือพื้นที่ต้องห้าม
- กำหนดประเภท Collision ที่สามารถทะลุได้ให้ชัดเจน
- ห้ามทำให้ Projectile หลุดนอกพื้นที่เล่น

### Ultimate Skill — Silent Judgment
- ค้นหาศัตรูที่มีชีวิตทั้งหมดภายในพื้นที่ Gameplay ที่มองเห็น
- Lock-on ศัตรูแต่ละตัว
- สร้างหอกเวทมนตร์ขนาดใหญ่พุ่งลงจากท้องฟ้าใส่แต่ละเป้าหมาย
- สร้าง Magic Damage สูง
- การโจมตีเป็น Guaranteed Hit ต่อเป้าหมายที่ล็อกสำเร็จ

**Expected Behavior**
- ไม่ใช้ระบบ Accuracy หรือ Evasion กับการโจมตีนี้
- ศัตรูที่ถูก Lock-on ต้องได้รับ Damage ตามเงื่อนไขของสกิล
- มี Visual Warning และ Spear Impact Effect
- หลีกเลี่ยงการสร้าง Effect เกินจำเป็นเมื่อมีศัตรูจำนวนมาก

---

## CHARACTER 05 — DAPHNE

**ชื่อ:** ดาฟนี่ (Daphne)

**ฉายา:** แม่มดแห่งดวงดารา

**Combat Role:** Hybrid Magic DPS / Stance Switching / Crowd Control / Self Sustain

**แนวทางการเล่น:** แม่มดที่สามารถสลับพลังเวทมนตร์ระหว่างดวงอาทิตย์และดวงจันทร์ ทำให้รูปแบบการโจมตีและสกิลเปลี่ยนไปตามโหมดที่ใช้งาน

### Weapon: Dual Celestial Staff
- คทาเวทมนตร์พลังสองขั้ว
- รองรับ Solar Mode และ Lunar Mode
- ต้องมีการแสดงสถานะโหมดปัจจุบันผ่าน UI หรือ Visual Effect

### Stance System
สร้างหรือเชื่อมต่อระบบสลับโหมด โดยมี 2 สถานะ

**Solar Mode (สีส้ม)**
- เน้น AoE Damage และการเผาไหม้
- แสดง VFX สีส้ม/ทอง

**Lunar Mode (สีม่วง)**
- เน้น Single Target Damage และ Crowd Control
- แสดง VFX สีม่วง

ต้องรองรับการสลับโหมดระหว่างการต่อสู้ โดยไม่รบกวนระบบโจมตีปกติและ Cooldown ที่มีอยู่

### Basic Attack — Dual Celestial Attack

**Solar Mode**
- ยิงลูกไฟเวทมนตร์
- เมื่อกระทบจะแตกกระจายสร้างความเสียหายหลายเป้าหมาย
- ติดสถานะ Burn ตามค่าที่กำหนดใน Config

**Lunar Mode**
- ยิงลำแสงจันทราเป็นเส้นตรง
- เน้น Damage ต่อเป้าหมายเดี่ยว
- มี Damage สูงกว่าการโจมตีเป้าหมายเดี่ยวของ Solar Mode

### Active Skill — Celestial Control

**Solar Mode: Solar Ring**
- สร้างวงแหวนเปลวไฟรอบตัวละคร
- สร้างความเสียหายต่อศัตรูใกล้เคียง
- ทำให้ศัตรูถูก Knockback ออกจากตัวละคร

**Lunar Mode: Moonlight Bind**
- ยิงแสงจันทราใส่เป้าหมาย
- ทำให้ศัตรูหยุดเคลื่อนที่ชั่วคราว
- ต้องรองรับเป้าหมายที่บินได้ เช่น Dragon และ Wyvern
- ไม่บังคับให้ศัตรูบินตกลงพื้น เว้นแต่ระบบเกมมี Mechanic นี้อยู่แล้ว

### Passive Skill — Celestial Transition
เมื่อเปลี่ยนโหมดจะได้รับ Buff ชั่วคราว

**Switch to Solar**
- เพิ่ม Attack Power ชั่วคราว

**Switch to Lunar**
- เพิ่ม Movement Speed ชั่วคราว

**Implementation Requirements**
- Buff ต้องหมดอายุและคืนค่าตามระบบอย่างถูกต้อง
- ป้องกันการสลับโหมดรัวเพื่อสะสม Buff แบบไม่จำกัด
- มี Feedback ระหว่างสลับโหมด

### Ultimate Skill — Eclipse
- ผสานพลัง Solar และ Lunar
- สร้างพายุพลังงานสีส้มและสีม่วง
- สร้าง AoE Magic Damage ทั่วพื้นที่ Gameplay ที่กำหนด
- ฟื้นฟู HP ให้ตัวละคร Daphne
- แสดง Eclipse VFX
- การใช้ Ultimate ต้องไม่บังคับเปลี่ยนโหมดปัจจุบัน เว้นแต่จำเป็นต่อระบบเดิม

---

# 3. Shared Combat System Requirements

## 3.1 Damage System

สกิลต้องรองรับ Damage Type และรูปแบบความเสียหายดังนี้

- Physical Damage
- Magic Damage
- Area of Effect Damage (AoE)
- Damage over Time (DoT)
- Armor Penetration / Defense Bypass
- Shield Absorption
- Guaranteed Hit

ต้องเชื่อมต่อกับ Damage System เดิม และป้องกัน Damage ถูกประมวลผลซ้ำโดยไม่ตั้งใจ

## 3.2 Status Effect System

รองรับ Status Effect ดังต่อไปนี้

| Status Effect | Behavior |
|---|---|
| Root | หยุดการเคลื่อนที่ |
| Slow | ลด Movement Speed |
| Knockback | ผลักศัตรูออก |
| Knock-up | ทำให้ศัตรูลอยและเสียจังหวะ |
| Burn | สร้าง Damage ต่อเนื่อง |
| Attack Reduction | ลด Attack Power |
| Defense Buff | เพิ่มพลังป้องกัน |
| Attack Speed Buff | เพิ่มความเร็วโจมตี |
| Attack Buff | เพิ่มพลังโจมตี |
| Movement Speed Buff | เพิ่มความเร็วเคลื่อนที่ |
| Shield | ดูดซับ Damage ก่อน HP |

Status Effect ต้องมีระบบ Duration, Expiration, Stacking Rules และ Cleanup ที่ถูกต้องตาม Architecture ของโปรเจกต์

## 3.3 Skill Configuration

ให้ Skill Parameters สามารถแก้ไขได้โดยไม่ต้องแก้ Core Logic เช่น

- Base Damage
- Damage Multiplier
- Cooldown
- Duration
- Attack Range
- AoE Radius
- Projectile Speed
- Hit Count
- Status Effect Duration
- Buff / Debuff Percentage
- Max Stack
- Shield Capacity
- Healing Amount
- Stance Switching Cooldown

**หมายเหตุ:** ค่าที่ไม่ได้ระบุใน Specification นี้ให้ตรวจสอบค่าที่เกมมีอยู่ก่อน หากไม่มีจึงกำหนดเป็นค่าเริ่มต้นชั่วคราวที่สมเหตุสมผล พร้อมระบุว่าค่าใดเป็น Placeholder สำหรับปรับสมดุลเกมภายหลัง

## 3.4 Animation & Visual Effects

ทุกสกิลควรมีการตอบสนองทางภาพที่ช่วยให้ผู้เล่นเข้าใจการทำงาน ได้แก่

- Casting Animation
- Projectile Effect
- Impact Effect
- Buff / Debuff Indicators
- Area Indicator
- Ultimate Effect
- Cooldown Feedback

หากไม่มี Asset ให้ใช้ Placeholder ที่เหมาะสม และออกแบบโครงสร้างให้เปลี่ยนเป็น Asset จริงได้ในอนาคต

## 3.5 Combat Integration

ระบบสกิลต้องรองรับ

- Player Input
- Character Switching (ถ้าเกมมีอยู่)
- Enemy Targeting
- Collision Detection
- Damage Calculation
- HP and Shield System
- Buff and Debuff System
- Cooldown Management
- Animation and VFX
- Enemy Death Handling
- Enemy AI Interaction

---

# 4. Implementation Workflow

ให้ดำเนินการตามลำดับดังต่อไปนี้

**Phase 1 — Project Analysis**
1. ตรวจสอบโครงสร้างโปรเจกต์ทั้งหมดในส่วนที่เกี่ยวข้อง
2. ค้นหาระบบ Character, Combat, Skill, Projectile, Enemy และ Status Effect
3. ระบุระบบเดิมที่สามารถนำกลับมาใช้ได้
4. ตรวจสอบ Input Mapping และวิธีเปิดใช้งานแต่ละสกิล
5. สรุปไฟล์ที่จะต้องแก้ไขหรือเพิ่มเติม

**Phase 2 — Core Integration**
1. เพิ่มเฉพาะความสามารถพื้นฐานที่ยังขาดจากระบบเดิม
2. เชื่อมต่อ Damage, Status Effect และ Skill Cooldown
3. เตรียม Skill Configuration
4. ตรวจสอบ Compatibility กับระบบเดิม

**Phase 3 — Character Skill Implementation**

พัฒนาตามลำดับดังนี้

1. Tanky — Defensive Skills
2. Shadow — High DPS Skills
3. Magician — Ricochet & Trap Skills
4. Oracle Witch — Magic Control Skills
5. Daphne — Dual Stance Skills

สำหรับแต่ละตัวละครให้ดำเนินการตามลำดับ

1. Implement Basic Attack
2. Implement Active Skill
3. Implement Passive Skill
4. Implement Ultimate Skill
5. Connect Animation / VFX
6. Test Skill Behavior

**Phase 4 — Validation & Testing**
- ตรวจสอบการทำงานของทั้ง 5 ตัวละคร
- ทดสอบ Damage Calculation
- ทดสอบ Skill Cooldown
- ทดสอบ Status Effect Duration
- ทดสอบ Skill Interaction กับศัตรูประเภทต่างๆ
- ทดสอบ Edge Cases และ Performance
- ตรวจสอบว่าระบบเดิมยังทำงานได้ตามปกติ

---

# 5. Acceptance Criteria

งานจะถือว่าสำเร็จเมื่อ

- [ ] ตัวละครทั้ง 5 ตัวมี Basic Attack, Active Skill, Passive Skill และ Ultimate Skill ที่ใช้งานได้จริง
- [ ] ทุกสกิลทำงานตาม Character Specifications
- [ ] Skill Cooldown ทำงานถูกต้อง
- [ ] Damage และ Status Effect ถูกคำนวณถูกต้อง
- [ ] Passive Skill ทำงานอัตโนมัติตามเงื่อนไข
- [ ] ไม่มี Buff หรือ Debuff ค้างหลังหมดระยะเวลา
- [ ] ไม่มี Duplicate Damage หรือ Projectile ที่ไม่ถูก Cleanup
- [ ] รองรับศัตรูหลายตัวพร้อมกัน
- [ ] สกิลที่โจมตีทั่วหน้าจอคำนวณขอบเขตเป้าหมายได้ถูกต้อง
- [ ] ไม่มี Error หรือ Runtime Exception จากการใช้สกิล
- [ ] ไม่ทำให้ระบบเดิมของเกมเสียหาย
- [ ] มีการทดสอบตามรูปแบบที่โปรเจกต์รองรับ และรายงานผลการทดสอบจริง
- [ ] ค่า Skill Parameters สามารถแก้ไขได้ง่าย

---

# 6. Expected Deliverables

เมื่อพัฒนาเสร็จ ให้สรุปผลดังนี้

1. **Implementation Summary:** ระบบและสกิลที่พัฒนาเสร็จแล้ว
2. **Modified Files:** รายชื่อไฟล์ที่เพิ่มหรือแก้ไข พร้อมเหตุผล
3. **Skill Implementation Status:** สถานะสกิลทั้ง 20 รายการ (5 ตัวละคร × 4 ความสามารถ)
4. **Configuration:** ตำแหน่งไฟล์และวิธีปรับแต่งค่าสกิล
5. **Testing Results:** การทดสอบที่รันจริง พร้อมผลลัพธ์
6. **Known Issues:** ข้อจำกัดหรือปัญหาที่พบ
7. **Remaining Work:** งานที่ยังไม่เสร็จหรือจำเป็นต้องใช้ Asset เพิ่มเติม

## Final Instruction

Implement these character skills directly into the existing game project. Prioritize functional gameplay, correct combat mechanics, maintainability, performance, and compatibility with existing systems.

Do not only create documentation or pseudocode. Write and integrate the actual implementation code.

Do not claim any skill is complete unless its logic is connected to the game and its behavior has been verified through the available tests.

If a requirement conflicts with the current architecture, prefer the least invasive compatible solution and document the deviation clearly.