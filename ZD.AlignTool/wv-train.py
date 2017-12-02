import gensim, logging
import io
logging.basicConfig(format='%(asctime)s : %(levelname)s : %(message)s', level=logging.INFO)
 
sentences = []
f = io.open('10-jiestem-for-w2v.txt', encoding='utf-8')
for line in f:
  sentences.append(line.split())

model = gensim.models.Word2Vec(sentences, min_count=3, size=200, workers=8, window=60, sg=1)
model.save('10-jiestem-wv-model.bin')
model.wv.save_word2vec_format('10-jiestem-wv.txt')