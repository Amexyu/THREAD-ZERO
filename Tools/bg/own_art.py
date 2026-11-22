# 让这些装备用自己的图（GameModel.cs 里把借图 art 改成 null）。用法：python3 own_art.py h15 h16 …
import re, sys, os
p = os.path.expanduser('~/mnt/THREAD-ZERO/ThreadZero/Assets/Scripts/GameModel.cs'); s = open(p, encoding='utf-8').read(); n = 0
for pid in sys.argv[1:]:
    s, k = re.subn(r'(new EquipmentBase\("%s",[^\n]*?AttackStyle\.\w+), "\w+", null\)' % pid, r'\1, null, null)', s); n += k
open(p, 'w', encoding='utf-8').write(s); print('改了', n, '件')
