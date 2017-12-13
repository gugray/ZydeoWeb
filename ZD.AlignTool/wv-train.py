import gensim, logging
import io
logging.basicConfig(format='%(asctime)s : %(levelname)s : %(message)s', level=logging.INFO)
 
sentences = []
f = io.open('41-zh-hu-tok-true.txt', encoding='utf-8')
for line in f:
  split = line.split()
  sentences.append(split)

model = gensim.models.Word2Vec(sentences, min_count=5, size=300, workers=8, window=65, sg=1)
model.save('43-wv-model.bin')
model.wv.save_word2vec_format('43-wv-model.txt')