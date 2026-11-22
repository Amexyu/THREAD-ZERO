# 打包进游戏的字体（v0.21，用户 11-22 选的样本 B：思源黑体 + Oxanium）。
#   思源黑体 = Noto Sans CJK（OFL），来源：工作区 /usr/share/fonts/opentype/noto 的 TTC，按语言拆成 SC / JP / KR 三个字体（各用各地区的字形），
#   再裁成用得到的字：各语言的常用字表（GB2312 / JIS X 0208 / KS X 1001）+ 游戏里所有文字（脚本里的字符串、翻译表）。
#   Oxanium（OFL）：Google Fonts 的可变字体（Tools/fonts/Oxanium-VF.ttf），实例化成 SemiBold / Bold 两个静态字体，只用来写数字和英文编号。
# 翻译表改了以后重跑一次：python3 Tools/fonts/build_fonts.py
import os, glob
from fontTools.ttLib import TTCollection, TTFont
from fontTools import subset
from fontTools.varLib import instancer

HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Fonts')
TTC = {'Regular': '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc', 'Bold': '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'}
FACE = {'JP': 0, 'KR': 1, 'SC': 2}

def rng(a, b): return {chr(c) for c in range(a, b + 1)}
def table(codec, rows, cols):
    out = set()
    for b1 in rows:
        for b2 in cols:
            try: out.add(bytes([b1, b2]).decode(codec))
            except UnicodeDecodeError: pass
    return out
BASIC = (rng(0x20, 0x7E) | rng(0xA0, 0x17F) | rng(0x2000, 0x206F) | rng(0x2100, 0x214F) | rng(0x2150, 0x218F) | rng(0x2190, 0x21FF)
         | rng(0x2200, 0x22FF) | rng(0x2460, 0x24FF) | rng(0x2500, 0x27BF) | rng(0x3000, 0x30FF) | rng(0x31F0, 0x31FF) | rng(0xFF00, 0xFFEF))
GB2312 = table('gb2312', range(0xA1, 0xF8), range(0xA1, 0xFF))
JIS = table('euc_jp', range(0xA1, 0xF5), range(0xA1, 0xFF))
KSX = table('euc_kr', range(0xA1, 0xFE), range(0xA1, 0xFF))

def corpus():
    text = ''
    for pat in ['ThreadZero/Assets/Scripts/**/*.cs', 'ThreadZero/Assets/Resources/Loc/*.txt', 'ThreadZero/Assets/Resources/**/*.json']:
        for f in glob.glob(os.path.join(ROOT, pat), recursive=True):
            try: text += open(f, encoding='utf-8').read()
            except Exception: pass
    return {ch for ch in text if ord(ch) >= 0x2E80}

def build():
    os.makedirs(OUT, exist_ok=True)
    used = corpus()
    sets = {'SC': BASIC | GB2312 | used, 'JP': BASIC | JIS | used, 'KR': BASIC | KSX | used}
    for lang, chars in sets.items():
        for weight, path in TTC.items():
            font = TTCollection(path).fonts[FACE[lang]]
            # layout_features 只留空：游戏里不用 OpenType 特性（地区字形已经靠拆成 SC / JP / KR 三个字体解决），不留就不会带上一万多个替换字形。
            # retain_gids：字形编号保持和原字体一样。不这样的话，每次重裁（字多了几个）后面所有字形的编号都会挪位，
            # Unity 编辑器不重启时还留着按旧编号缓存的字形，个别字就会画成别的字（11-23 遇到：日文「私」画成「秒」、韩文字体里「種」画成「窗」）。
            opts = subset.Options(); opts.layout_features = []; opts.name_IDs = ['*']; opts.name_languages = ['*']; opts.notdef_outline = True; opts.glyph_names = False; opts.retain_gids = True
            sub = subset.Subsetter(opts); sub.populate(text=''.join(sorted(chars))); sub.subset(font)
            dst = os.path.join(OUT, 'NotoSans%s-%s.otf' % (lang, weight)); font.save(dst)
            print(os.path.basename(dst), len(chars), 'chars', os.path.getsize(dst) // 1024, 'KB')
    for weight, w in (('SemiBold', 600), ('Bold', 700)):
        vf = TTFont(os.path.join(HERE, 'Oxanium-VF.ttf'))
        st = instancer.instantiateVariableFont(vf, {'wght': w}, updateFontNames=True)
        dst = os.path.join(OUT, 'Oxanium-%s.ttf' % weight); st.save(dst); print(os.path.basename(dst), os.path.getsize(dst) // 1024, 'KB')

if __name__ == '__main__': build()
