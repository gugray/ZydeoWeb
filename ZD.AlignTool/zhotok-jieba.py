#encoding=utf-8
import jieba
import io
import sys
import codecs

#jieba.initialize()
#jieba.set_dictionary('../_work_align/dict.txt.big')
#seg_list = jieba.cut("我来到北京清华大学", cut_all=True)
#print("Full Mode: " + "/ ".join(seg_list))  # 全模式

if __name__ == '__main__':
    jieba.initialize()
    jieba.set_dictionary('../_work_align/dict.txt.big')

    # python 2/3 compatibility
    if sys.version_info < (3, 0):
        sys.stderr = codecs.getwriter('UTF-8')(sys.stderr)
        sys.stdout = codecs.getwriter('UTF-8')(sys.stdout)
        sys.stdin = codecs.getreader('UTF-8')(sys.stdin)
    else:
        sys.stderr = codecs.getwriter('UTF-8')(sys.stderr.buffer)
        sys.stdout = codecs.getwriter('UTF-8')(sys.stdout.buffer)
        sys.stdin = codecs.getreader('UTF-8')(sys.stdin.buffer)

    for line in sys.stdin:
      seg_list = jieba.cut(line, cut_all=False)
      sys.stdout.write(" ".join(seg_list))
      #sys.stdout.write("\n")
