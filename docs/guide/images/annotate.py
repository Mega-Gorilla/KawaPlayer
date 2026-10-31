"""Draws the marks on the guide's pictures.

Each picture in vcc/, unity/, screen/ and tablet/ is the one of the same name in
original/, which has no marks, with what MARKS lists drawn on it: an outline around
what to press or fill in, a numbered badge, and a caption with an arrow to the
outline. Coordinates are pixels of the original. After changing MARKS or replacing
an original, run:

  python annotate.py <font> [name ...]

<font> is Zen Maru Gothic Regular (https://fonts.google.com/specimen/Zen+Maru+Gothic).
Needs Pillow. Without names, every picture is made again.

The pictures are WebP: those of VCC and Unity at their captured size, lossless or
quality 90, whichever is smaller; those of the world and of VRChat's menus at most 1280
wide (as the originals are), at quality 80.
"""
import io
import math
import os
import sys
from collections import Counter

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ACCENT = (255, 102, 0)
WHITE = (255, 255, 255)
SS = 2  # Marks are drawn at twice the size and reduced, so their edges are smooth.

# name: src (the original to draw on, if not the one of the same name), crop (the part
# of the original to keep), pad (margin added around it, for captions), scale (size of
# the marks), marks. A mark: box, n (badge), badge (where, if not the box's top-left;
# a badge alone needs no box), cap (caption) and at (its centre). Coordinates are those
# of the original, before crop and pad.
MARKS = {
    "vcc/listing-page": dict(scale=0.9, marks=[
        dict(box=(526, 168, 640, 204), cap="ここを押す", at=(290, 186)),
    ]),
    "vcc/add-repository": dict(scale=0.72, pad=dict(bottom=90), marks=[
        dict(box=(236, 108, 302, 136), n=1),
        dict(box=(914, 172, 1046, 198), n=2),
        dict(box=(166, 250, 818, 286), n=3, cap="URL を貼る", at=(420, 350)),
        dict(box=(858, 250, 954, 284), n=4, cap="押す", at=(906, 350)),
    ]),
    "vcc/repository-list": dict(scale=0.6, pad=dict(top=8, bottom=70, left=8, right=8), marks=[
        dict(box=(16, 16, 196, 43), cap="これがあれば追加できている", at=(330, 92)),
    ]),
    "vcc/add-package": dict(scale=0.8, pad=dict(bottom=64), marks=[
        dict(box=(1203, 13, 1239, 49), cap="＋ を押す", at=(1060, 100)),
    ]),
    "vcc/update-package": dict(scale=0.8, pad=dict(bottom=70), marks=[
        dict(box=(691, 9, 1009, 55), cap="押すと更新", at=(560, 104)),
    ]),
    # Updating KawaPlayer in a project on an older SDK: VCC lists the SDK too (#193).
    "vcc/update-confirm-sdk": dict(crop=(0, 10, 900, 496), scale=0.8, pad=dict(right=290), marks=[
        dict(box=(39, 261, 849, 390), n=1, cap="SDK もいっしょに上がる", at=(1050, 325)),
        dict(box=(719, 411, 863, 459), n=2, cap="押すと更新", at=(1050, 435)),
    ]),
    "unity/gameobject-menu": dict(scale=0.7, pad=dict(top=24, left=24, right=12), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 447, 276), n=2),
        dict(box=(457, 244, 742, 276), n=3, badge=(746, 240)),
    ]),
    "unity/gameobject-menu-tablet": dict(src="unity/gameobject-menu", scale=0.7, pad=dict(top=24, left=24, right=12), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 447, 276), n=2),
        dict(box=(457, 396, 742, 427), n=3, badge=(746, 392)),
    ]),
    "unity/gameobject-menu-global-reload": dict(src="unity/gameobject-menu", scale=0.7, pad=dict(top=24, left=24, right=12), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 447, 276), n=2),
        dict(box=(457, 431, 742, 462), n=3, badge=(746, 427)),
    ]),
    "unity/hierarchy-empty": dict(scale=0.7, marks=[
        dict(box=(16, 196, 360, 318), cap="何もないところを押す", at=(188, 368)),
    ]),
    "unity/hierarchy-menu": dict(scale=0.7, pad=dict(left=16, right=12), marks=[
        dict(box=(8, 190, 346, 224), n=1),
        dict(box=(355, 192, 641, 224), n=2, badge=(645, 188)),
    ]),
    "unity/inspector": dict(scale=0.72, marks=[
        dict(box=(285, 360, 581, 385), n=1),
        dict(box=(24, 428, 832, 548), n=2),
        dict(box=(24, 563, 832, 945), n=3),
        dict(box=(24, 961, 832, 1051), n=4),
        dict(box=(24, 1067, 832, 1207), n=5),
        dict(box=(24, 1223, 832, 1380), n=6),
    ]),
    # The settings page shows each part of the inspector under its heading, with the
    # number it has on the whole inspector.
    "unity/inspector-language": dict(src="unity/inspector", crop=(196, 338, 676, 400), scale=0.72, marks=[
        dict(n=1, badge=(260, 372)),
    ]),
    "unity/inspector-appearance": dict(src="unity/inspector", crop=(3, 418, 841, 556), scale=0.72, pad=dict(left=28), marks=[
        dict(n=2, badge=(-11, 441)),
    ]),
    "unity/inspector-player": dict(src="unity/inspector", crop=(3, 553, 841, 953), scale=0.72, pad=dict(left=28), marks=[
        dict(n=3, badge=(-11, 576)),
    ]),
    "unity/inspector-playlist": dict(src="unity/inspector", crop=(3, 951, 841, 1059), scale=0.72, pad=dict(left=28), marks=[
        dict(n=4, badge=(-11, 974)),
    ]),
    "unity/inspector-modules": dict(src="unity/inspector", crop=(3, 1057, 841, 1215), scale=0.72, pad=dict(left=28), marks=[
        dict(n=5, badge=(-11, 1080)),
    ]),
    "unity/inspector-version": dict(src="unity/inspector", crop=(3, 1213, 841, 1391), scale=0.72, pad=dict(left=28), marks=[
        dict(n=6, badge=(-11, 1236)),
    ]),
    # The first look at the editor, with nothing to press yet.
    "unity/playlist-editor": dict(scale=0.8, marks=[]),
    "unity/playlist-import": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(855, 37, 1008, 62), n=1),
        dict(box=(197, 76, 1178, 101), n=2, cap="VHub の VRChat用URL を貼る", at=(960, 320)),
        dict(box=(1186, 76, 1339, 101), n=3),
        dict(box=(1302, 37, 1346, 62), n=4),
    ]),
    "unity/playlist-manual": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(470, 288, 510, 315), n=1, cap="プレイリストを足す", at=(300, 380)),
        dict(box=(1241, 320, 1283, 347), n=2, cap="曲を足す", at=(1080, 420)),
        dict(box=(592, 222, 1333, 310), n=3),
        dict(box=(1302, 37, 1346, 62), n=4),
    ]),
    # The toolbar and the panel each button opens, cut from the top of the editor.
    "unity/playlist-ytdlp": dict(crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(709, 37, 852, 62), n=1),
        dict(box=(12, 138, 134, 160), n=2, badge=(156, 149)),
        dict(box=(1193, 137, 1339, 160), n=3),
    ]),
    "unity/playlist-json-import": dict(crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(1011, 37, 1161, 62), n=1),
        dict(box=(1186, 137, 1339, 160), n=2),
    ]),
    # Export JSON's panel shows in the picture of the hand-made playlist.
    "unity/playlist-export": dict(src="unity/playlist-manual", crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(1165, 37, 1297, 62), n=1),
        dict(box=(1204, 137, 1339, 160), n=2),
    ]),
    # The track list of a real playlist, which the owner allowed for this picture only.
    "unity/playlist-tracks": dict(crop=(582, 62, 1352, 482), scale=0.8, pad=dict(left=16), marks=[
        dict(box=(1251, 68, 1346, 94), n=1, badge=(1234, 81)),
        dict(box=(592, 121, 612, 136), n=2),
        dict(box=(741, 115, 940, 140), n=3),
        dict(box=(741, 144, 1313, 201), n=4, badge=(726, 172)),
        dict(box=(1285, 118, 1310, 138), n=5, badge=(1266, 128)),
    ]),
    "unity/module-manager": dict(scale=0.72, marks=[
        dict(box=(28, 423, 834, 537), n=1),
        dict(box=(746, 615, 828, 1222), n=2),
    ]),
    # The quickstart only looks at the KawaPlayer field, which placing it fills in with one
    # player in the scene (#267); the tablet page explains the rest.
    "unity/tablet-player-field": dict(src="unity/tablet-inspector", scale=0.72, pad=dict(right=270), marks=[
        dict(box=(274, 430, 664, 456), cap="操作する KawaPlayer", at=(826, 443)),
    ]),
    "unity/build": dict(scale=0.72, pad=dict(left=16), marks=[
        dict(box=(9, 257, 404, 288), n=1),
        dict(box=(10, 304, 631, 402), n=2),
    ]),
    # Each module's settings, cut from its inspector below the U# header (the modules page).
    "unity/module-default-url": dict(crop=(6, 289, 840, 365), scale=0.72, marks=[]),
    "unity/module-playlist-loader": dict(crop=(6, 293, 840, 1045), scale=0.72, marks=[]),
    "unity/module-autoplay-track": dict(crop=(6, 368, 840, 623), scale=0.72, marks=[]),
    "unity/module-autoplay-playlist": dict(crop=(6, 368, 840, 632), scale=0.72, marks=[]),
    "unity/module-permission": dict(crop=(6, 398, 840, 761), scale=0.72, marks=[]),
    "unity/module-persistence": dict(crop=(6, 368, 840, 542), scale=0.72, marks=[]),
    "unity/module-slideshow": dict(crop=(6, 398, 840, 473), scale=0.72, marks=[]),
    "unity/module-timeline-sync": dict(crop=(6, 368, 840, 602), scale=0.72, marks=[]),
    "unity/project-udonsharp-program": dict(scale=0.72, marks=[
        dict(box=(66, 620, 200, 644), n=1),
        dict(box=(398, 183, 520, 208), n=2, cap="U# のアイコンのほう", at=(760, 196)),
    ]),
    "unity/inspector-compile-all": dict(crop=(0, 0, 690, 245), scale=0.72, pad=dict(bottom=56), marks=[
        dict(box=(28, 148, 683, 173), n=3, cap="押す", at=(345, 268)),
    ]),
    "unity/console-compile-finished": dict(scale=0.72, pad=dict(top=8, bottom=56, right=60), marks=[
        dict(box=(150, 6, 650, 29), cap="これが出れば終わり", at=(400, 86)),
    ]),
    "vrchat/quick-menu": dict(scale=0.8, pad=dict(bottom=64), marks=[
        dict(box=(736, 522, 796, 582), n=1, cap="歯車を押す", at=(600, 640)),
    ]),
    "vrchat/quick-menu-settings": dict(scale=0.8, marks=[
        dict(box=(772, 18, 822, 66), n=2, badge=(748, 42), cap="全画面にする", at=(560, 140)),
    ]),
    "vrchat/untrusted-urls": dict(scale=0.8, marks=[
        dict(box=(12, 338, 240, 393), n=3, badge=(244, 334)),
        dict(box=(986, 334, 1068, 378), n=4, cap="オンにする", at=(800, 357)),
    ]),
    "vrchat/site-menu": dict(scale=0.8, pad=dict(left=16), marks=[
        dict(box=(8, 169, 388, 233), n=1),
    ]),
    "vrchat/site-my-worlds": dict(scale=0.8, pad=dict(left=12), marks=[
        dict(box=(22, 178, 588, 668), n=2),
    ]),
    "vrchat/site-edit-world-info": dict(scale=0.8, marks=[]),
    "vrchat/site-allowed-domains": dict(scale=0.9, marks=[
        dict(box=(31, 149, 1115, 203), n=3, cap="playlist.vrc-hub.com を入れる", at=(745, 119)),
    ]),
    "vhub/top-search": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(309, 223, 1250, 278), n=1),
    ]),
    "vhub/top-login": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(1169, 12, 1250, 52), n=1, badge=(1158, 68)),
        dict(box=(1082, 12, 1160, 52), n=2, badge=(1072, 68)),
    ]),
    "vhub/detail": dict(scale=0.8, marks=[
        dict(box=(18, 432, 611, 504), n=1),
    ]),
    "vhub/detail-copied": dict(scale=0.8, marks=[]),
    "vhub/create-name": dict(scale=0.8, marks=[
        dict(box=(10, 36, 728, 103), n=1),
        dict(box=(14, 136, 138, 163), n=2),
    ]),
    "vhub/create-videos": dict(scale=0.8, marks=[
        dict(box=(34, 176, 599, 230), n=1),
        dict(box=(613, 176, 703, 230), n=2),
    ]),
    "vhub/create-classify": dict(scale=0.8, marks=[
        dict(box=(34, 118, 703, 179), n=1),
        dict(box=(34, 453, 216, 495), n=2),
        dict(box=(30, 552, 706, 668), n=3),
    ]),
    "vhub/create-button": dict(scale=0.8, marks=[
        dict(box=(9, 12, 729, 77), n=1),
    ]),
    "vhub/created-url": dict(scale=0.8, marks=[
        dict(box=(343, 328, 1110, 378), n=1),
        dict(box=(1122, 323, 1265, 383), n=2),
    ]),
    "vhub/edit-button": dict(scale=0.8, marks=[
        dict(box=(216, 132, 350, 204), n=1),
    ]),
    "vhub/top-library": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(258, 12, 377, 52), n=1),
    ]),
    "vhub/edit-videos": dict(scale=0.8, marks=[
        dict(box=(48, 284, 164, 454), n=1),
        dict(box=(1160, 292, 1222, 331), n=2),
    ]),
    "vhub/edit-save": dict(scale=0.8, pad=dict(top=14), marks=[
        dict(box=(1126, 8, 1272, 74), n=1),
    ]),
    "vhub/delete": dict(scale=0.8, marks=[
        dict(box=(35, 110, 307, 166), n=1),
    ]),
    "screen/controls": dict(scale=0.667, marks=[
        dict(box=(30, 24, 224, 116), n=1),
        dict(box=(34, 565, 1250, 603), n=2),
        dict(box=(36, 617, 350, 689), n=3),
        dict(box=(462, 607, 818, 699), n=4),
        dict(box=(932, 617, 1244, 689), n=5),
    ]),
    # The bar's three groups, each on the bottom strip of the same picture.
    "screen/controls-left": dict(src="screen/controls", crop=(0, 530, 1280, 720), scale=0.667, marks=[
        dict(box=(40, 621, 104, 685), n=1),
        dict(box=(120, 621, 184, 685), n=2),
        dict(box=(200, 621, 264, 685), n=3),
        dict(box=(280, 621, 344, 685), n=4),
    ]),
    "screen/controls-middle": dict(src="screen/controls", crop=(0, 530, 1280, 720), scale=0.667, marks=[
        dict(box=(34, 565, 1250, 603), n=1),
        dict(box=(475, 621, 539, 685), n=2),
        dict(box=(554, 609, 642, 697), n=3),
        dict(box=(657, 621, 721, 685), n=4),
        dict(box=(741, 621, 805, 685), n=5),
    ]),
    "screen/controls-right": dict(src="screen/controls", crop=(0, 530, 1280, 720), scale=0.667, marks=[
        dict(box=(936, 621, 1000, 685), n=1),
        dict(box=(1016, 621, 1080, 685), n=2),
        dict(box=(1096, 621, 1160, 685), n=3),
        dict(box=(1176, 621, 1240, 685), n=4),
    ]),
    # The technical page on sync points at ReSync alone.
    "screen/resync": dict(src="screen/controls", crop=(0, 450, 700, 720), scale=0.667, marks=[
        dict(box=(280, 621, 344, 685), cap="ReSync（押した人の画面だけ）", at=(470, 500)),
    ]),
    "screen/volume": dict(crop=(700, 100, 1280, 720), scale=0.667, marks=[
        dict(box=(930, 158, 1004, 200), n=1),
        dict(box=(944, 208, 992, 456), n=2),
        dict(box=(942, 478, 994, 530), n=3),
    ]),
    "screen/more": dict(crop=(700, 380, 1280, 720), scale=0.667, marks=[
        dict(box=(1102, 489, 1154, 541), n=1),
        dict(box=(1174, 489, 1226, 541), n=2),
    ]),
    "screen/playlist-list": dict(scale=0.667, marks=[
        dict(box=(821, 21, 964, 67), n=1),
        dict(box=(968, 21, 1111, 67), n=2),
        dict(box=(1115, 21, 1259, 67), n=3),
        dict(box=(817, 84, 1260, 148), n=4, cap="押すと曲の一覧へ", at=(600, 116)),
        dict(box=(817, 219, 1260, 283), n=5, cap="VHub のプレイリストを足す", at=(600, 251)),
        dict(box=(1196, 620, 1262, 686), n=6, cap="閉じる", at=(1080, 560)),
    ]),
    "screen/playlist-tracks": dict(scale=0.667, marks=[
        dict(box=(829, 93, 873, 137), n=1),
        dict(box=(895, 98, 1145, 135), n=2),
        dict(box=(827, 231, 1253, 297), n=3, cap="曲を押す", at=(640, 264)),
        dict(box=(827, 298, 1253, 365), n=4, cap="流れている曲", at=(640, 332)),
    ]),
    "screen/track-actions": dict(scale=0.667, marks=[
        dict(box=(853, 236, 905, 288), n=1),
        dict(box=(957, 236, 1009, 288), n=2),
        dict(box=(1059, 236, 1111, 288), n=3),
        dict(box=(1161, 234, 1217, 290), n=4),
    ]),
    "screen/queue": dict(scale=0.667, marks=[
        dict(box=(968, 21, 1111, 67), n=1),
        dict(box=(822, 84, 1258, 285), n=2),
    ]),
    "screen/queue-actions": dict(scale=0.667, marks=[
        dict(box=(854, 156, 906, 208), n=1),
        dict(box=(957, 156, 1009, 208), n=2),
        dict(box=(1060, 156, 1112, 208), n=3),
        dict(box=(1163, 156, 1215, 208), n=4),
    ]),
    "screen/history": dict(scale=0.667, marks=[
        dict(box=(1115, 21, 1259, 67), n=1),
        dict(box=(822, 84, 1258, 151), n=2),
    ]),
    "screen/settings": dict(scale=0.667, marks=[
        dict(box=(38, 11, 105, 77), n=1),
        dict(box=(32, 92, 250, 440), n=2, cap="分類を切り替える", at=(141, 520)),
        dict(box=(258, 645, 1194, 712), n=3),
    ]),
    "screen/settings-playback": dict(src="screen/settings", scale=0.667, marks=[
        dict(box=(270, 86, 1240, 181), n=1),
        dict(box=(270, 195, 1240, 343), n=2),
        dict(box=(270, 358, 1240, 568), n=3),
    ]),
    "screen/settings-playback2": dict(scale=0.667, marks=[
        dict(box=(270, 439, 1240, 655), n=4),
    ]),
    "screen/settings-video": dict(scale=0.667, marks=[
        dict(box=(270, 86, 1240, 233), n=1),
        dict(box=(270, 248, 1240, 343), n=2),
        dict(box=(270, 357, 1240, 517), n=3),
    ]),
    "screen/settings-audio": dict(scale=0.667, marks=[
        dict(box=(270, 86, 1240, 246), n=1),
    ]),
    "screen/settings-language": dict(scale=0.667, marks=[
        dict(box=(270, 86, 1240, 345), n=1),
    ]),
    # The autoplay card alone, cut from captures at 1920 x 1080, so at their own size.
    "screen/autoplay-owner": dict(scale=1.0, marks=[
        dict(box=(45, 226, 323, 311), n=1),
        dict(box=(333, 226, 548, 311), n=2),
    ]),
    "screen/autoplay-owner-set": dict(scale=1.0, marks=[
        dict(box=(42, 170, 712, 213), n=1),
    ]),
    "screen/autoplay-other": dict(scale=1.0, marks=[]),
    "screen/url-button": dict(scale=0.667, marks=[
        dict(box=(119, 78, 480, 106), n=1, badge=(484, 74), cap="入れられる URL", at=(700, 92)),
        dict(box=(38, 619, 106, 687), n=2, cap="ここを押す", at=(230, 540)),
    ]),
    "screen/url-confirm": dict(scale=0.667, marks=[
        dict(box=(318, 382, 762, 422), n=1),
        dict(box=(312, 438, 520, 485), n=2),
        dict(box=(536, 438, 744, 485), n=3),
        dict(box=(760, 438, 968, 485), n=4),
    ]),
    "screen/playlist-loaded": dict(scale=0.667, marks=[
        dict(box=(40, 621, 104, 685), n=1, cap="ここに URL を入れる", at=(260, 540)),
    ]),
    "screen/playlist": dict(scale=0.667, marks=[
        dict(box=(827, 164, 1253, 230), n=1, cap="曲を押す", at=(640, 197)),
        dict(box=(1207, 93, 1251, 137), n=2),
    ]),
    "screen/playlist-vhub": dict(scale=0.667, marks=[
        dict(box=(1207, 93, 1251, 137), n=1),
        dict(box=(832, 172, 874, 214), n=2),
        dict(box=(893, 172, 935, 214), n=3),
        dict(box=(1203, 168, 1251, 216), n=4),
    ]),
    "screen/global-reload": dict(scale=0.667, marks=[]),
    "screen/error": dict(scale=0.667, marks=[
        dict(box=(552, 505, 728, 543), cap="押す", at=(840, 524)),
    ]),
    "screen/error-details": dict(scale=0.667, marks=[
        dict(box=(312, 531, 968, 575), n=1, cap="Discord の URL をコピー", at=(1120, 480)),
        dict(box=(312, 592, 968, 639), n=2, cap="閉じる", at=(1120, 615)),
    ]),
    "screen/error-access-denied": dict(scale=0.667, marks=[
        dict(box=(312, 328, 895, 434), cap="ワールドの制作者向けの案内", at=(1110, 380)),
    ]),
    "tablet/home": dict(scale=0.72, marks=[
        dict(box=(138, 442, 1142, 645), n=1, cap="アプリを開く", at=(640, 400)),
        dict(box=(618, 720, 662, 766), n=2, cap="ホームボタン", at=(880, 744)),
    ]),
    "tablet/kawaplayer": dict(scale=0.72, marks=[
        dict(box=(108, 605, 172, 669), n=1, cap="URL を入れる", at=(320, 520)),
        dict(box=(610, 710, 670, 770), n=2, cap="ホームへ戻る", at=(880, 740)),
    ]),
    "tablet/rotate": dict(scale=0.72, marks=[]),
    "tablet/image": dict(scale=0.72, marks=[
        dict(box=(571, 74, 1176, 126), cap="画像の URL を入れる", at=(872, 184)),
    ]),
    "tablet/visitors": dict(scale=0.72, marks=[
        dict(box=(782, 72, 1176, 128), cap="一覧とログを切り替え", at=(880, 184)),
    ]),
    "tablet/settings": dict(scale=0.72, marks=[
        dict(box=(100, 143, 348, 515), cap="分類を切り替え", at=(224, 580)),
    ]),
}


def arrow(d, a, b, width):
    d.line([a, b], fill=ACCENT, width=width)
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    head = width * 4.2
    p1 = (b[0] - head * math.cos(ang - 0.45), b[1] - head * math.sin(ang - 0.45))
    p2 = (b[0] - head * math.cos(ang + 0.45), b[1] - head * math.sin(ang + 0.45))
    d.polygon([b, p1, p2], fill=ACCENT)


def nearest(box, p):
    return (min(max(p[0], box[0]), box[2]), min(max(p[1], box[1]), box[3]))


def draw_marks(size, marks, scale, font_path):
    """Draws the marks on a transparent layer SS times the picture's size."""
    layer = Image.new("RGBA", (size[0] * SS, size[1] * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    k = scale * SS
    w = max(3, round(4 * k))
    f_badge = ImageFont.truetype(font_path, round(26 * k))
    f_cap = ImageFont.truetype(font_path, round(28 * k))
    for m in marks:
        pad = 6 * k
        if "box" in m:
            b = tuple(v * SS for v in m["box"])
            b = (b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad)
            d.rounded_rectangle(b, radius=12 * k, outline=ACCENT, width=w)
        if m.get("cap"):
            cx, cy = (v * SS for v in m["at"])
            pw, ph = d.textlength(m["cap"], font=f_cap) + 30 * k, 46 * k
            pill = (cx - pw / 2, cy - ph / 2, cx + pw / 2, cy + ph / 2)
            d.rounded_rectangle(pill, radius=ph / 2, fill=ACCENT)
            d.text((cx, cy), m["cap"], font=f_cap, fill=WHITE, anchor="mm")
            tip = nearest(b, (cx, cy))
            start = nearest(pill, tip)
            # Stop a little short of the outline; draw no arrow when the caption is beside it.
            ang = math.atan2(tip[1] - start[1], tip[0] - start[0])
            tip = (tip[0] - w * 2 * math.cos(ang), tip[1] - w * 2 * math.sin(ang))
            if math.dist(start, tip) > w * 6:
                arrow(d, start, tip, w)
        if m.get("n"):
            r = 19 * k
            cx, cy = (v * SS for v in m["badge"]) if "badge" in m else (b[0], b[1])
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=ACCENT, outline=WHITE, width=max(2, round(2 * k)))
            d.text((cx, cy + k), str(m["n"]), font=f_badge, fill=WHITE, anchor="mm")
    # Reduced with the alpha premultiplied, so the edges do not darken.
    return layer.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def add_margin(im, top=0, bottom=0, left=0, right=0):
    """Adds room for captions, in the picture's most common colour."""
    color = Counter(im.getdata()).most_common(1)[0][0]
    out = Image.new("RGB", (im.width + left + right, im.height + top + bottom), color)
    out.paste(im, (left, top))
    return out


def shift(m, dx, dy):
    m = dict(m)
    if "box" in m:
        m["box"] = (m["box"][0] + dx, m["box"][1] + dy, m["box"][2] + dx, m["box"][3] + dy)
    for key in ("at", "badge"):
        if key in m:
            m[key] = (m[key][0] + dx, m[key][1] + dy)
    return m


def make(name, spec, font_path):
    im = Image.open(os.path.join(HERE, "original", spec.get("src", name) + ".webp")).convert("RGB")
    marks = spec["marks"]
    if "crop" in spec:
        c = spec["crop"]
        im = im.crop(c)
        marks = [shift(m, -c[0], -c[1]) for m in marks]
    if "pad" in spec:
        im = add_margin(im, **spec["pad"])
        marks = [shift(m, spec["pad"].get("left", 0), spec["pad"].get("top", 0)) for m in marks]
    im = Image.alpha_composite(im.convert("RGBA"), draw_marks(im.size, marks, spec["scale"], font_path)).convert("RGB")
    if name.split("/")[0] in ("vcc", "unity"):
        # Screenshots of VCC and Unity are small text on flat colours, which lossless keeps
        # sharp at about the size of quality 90; a capture with noise takes quality 90.
        options = (dict(lossless=True, quality=100), dict(quality=90))
    else:
        options = (dict(quality=80),)
    data = min((encode(im, **o) for o in options), key=len)
    path = os.path.join(HERE, name + ".webp")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    print(f"{name:26} {im.width}x{im.height} {len(data) // 1024} KB")


def encode(im, **options):
    buf = io.BytesIO()
    im.save(buf, "WEBP", method=6, **options)
    return buf.getvalue()


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    names = sys.argv[2:] or list(MARKS)
    for name in names:
        make(name, MARKS[name], sys.argv[1])
