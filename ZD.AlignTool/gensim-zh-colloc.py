import io
import gensim
from gensim.models import Phrases
from gensim.models.phrases import Phraser

sentences = []
f = io.open('../_work_align/20-zh-tocolloc.txt', encoding='utf-8')
for line in f:
  sentences.append(line.split())

phrases = Phrases(sentences, scoring='npmi', threshold=0.5)
g = io.open('../_work_align/21-tmp-zh-bigrams.txt', mode='w', encoding='utf-8')
# for key in phrases.vocab.keys():
#   g.write(key.encode('utf-8'))
#   g.write('\n')

for phrase, score in phrases.export_phrases(sentences):
  g.write(str(phrase, 'utf-8'))
  g.write('\t')
  g.write(str(score))
  g.write('\n')

phraser = Phraser(phrases)
h = io.open('../_work_align/21-tmp-zh-seg-bi.txt', mode='w', encoding='utf-8')
for sent in sentences:
  grams = phraser(sent)
  first = True
  for gram in grams:
    if not first: h.write(' ')
    first = False
    h.write(gram)
  h.write('\n')
