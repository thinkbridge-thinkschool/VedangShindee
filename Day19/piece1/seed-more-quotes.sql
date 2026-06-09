-- Adds 50 quotes from 10 real authors (keeps existing rows untouched)
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO Quotes (Author, Text, OwnerId, CreatedAt) VALUES

-- Maya Angelou
('Maya Angelou', 'I''ve learned that people will forget what you said, people will forget what you did, but people will never forget how you made them feel.', NULL, @Now),
('Maya Angelou', 'We may encounter many defeats but we must not be defeated.', NULL, @Now),
('Maya Angelou', 'If you don''t like something, change it. If you can''t change it, change your attitude.', NULL, @Now),
('Maya Angelou', 'Nothing will work unless you do.', NULL, @Now),
('Maya Angelou', 'A wise woman wishes to be no one''s enemy; a wise woman refuses to be anyone''s victim.', NULL, @Now),

-- Oscar Wilde
('Oscar Wilde', 'Be yourself; everyone else is already taken.', NULL, @Now),
('Oscar Wilde', 'To live is the rarest thing in the world. Most people just exist.', NULL, @Now),
('Oscar Wilde', 'Always forgive your enemies; nothing annoys them so much.', NULL, @Now),
('Oscar Wilde', 'I can resist everything except temptation.', NULL, @Now),
('Oscar Wilde', 'Experience is simply the name we give our mistakes.', NULL, @Now),

-- Mark Twain
('Mark Twain', 'The secret of getting ahead is getting started.', NULL, @Now),
('Mark Twain', 'If you tell the truth, you don''t have to remember anything.', NULL, @Now),
('Mark Twain', 'Kindness is the language which the deaf can hear and the blind can see.', NULL, @Now),
('Mark Twain', 'The two most important days in your life are the day you are born and the day you find out why.', NULL, @Now),
('Mark Twain', 'Twenty years from now you will be more disappointed by the things you didn''t do than by the ones you did.', NULL, @Now),

-- Abraham Lincoln
('Abraham Lincoln', 'In the end, it''s not the years in your life that count. It''s the life in your years.', NULL, @Now),
('Abraham Lincoln', 'Give me six hours to chop down a tree and I will spend the first four sharpening the axe.', NULL, @Now),
('Abraham Lincoln', 'Whatever you are, be a good one.', NULL, @Now),
('Abraham Lincoln', 'I am not bound to win, but I am bound to be true.', NULL, @Now),
('Abraham Lincoln', 'The best way to predict your future is to create it.', NULL, @Now),

-- Rumi
('Rumi', 'Out beyond ideas of wrongdoing and rightdoing, there is a field. I''ll meet you there.', NULL, @Now),
('Rumi', 'The wound is the place where the Light enters you.', NULL, @Now),
('Rumi', 'Don''t grieve. Anything you lose comes round in another form.', NULL, @Now),
('Rumi', 'Yesterday I was clever, so I wanted to change the world. Today I am wise, so I am changing myself.', NULL, @Now),
('Rumi', 'Sell your cleverness and buy bewilderment.', NULL, @Now),

-- Marie Curie
('Marie Curie', 'Nothing in life is to be feared, it is only to be understood. Now is the time to understand more, so that we may fear less.', NULL, @Now),
('Marie Curie', 'Be less curious about people and more curious about ideas.', NULL, @Now),
('Marie Curie', 'I was taught that the way of progress was neither swift nor easy.', NULL, @Now),
('Marie Curie', 'I have no dress except the one I wear every day. If you are going to be kind enough to give me one, let it be practical.', NULL, @Now),
('Marie Curie', 'Life is not easy for any of us. But what of that? We must have perseverance.', NULL, @Now),

-- Leonardo da Vinci
('Leonardo da Vinci', 'Learning never exhausts the mind.', NULL, @Now),
('Leonardo da Vinci', 'Simplicity is the ultimate sophistication.', NULL, @Now),
('Leonardo da Vinci', 'The noblest pleasure is the joy of understanding.', NULL, @Now),
('Leonardo da Vinci', 'Where the spirit does not work with the hand, there is no art.', NULL, @Now),
('Leonardo da Vinci', 'Iron rusts from disuse; water loses its purity from stagnation and in cold weather becomes frozen; even so does inaction sap the vigors of the mind.', NULL, @Now),

-- Eleanor Roosevelt
('Eleanor Roosevelt', 'The future belongs to those who believe in the beauty of their dreams.', NULL, @Now),
('Eleanor Roosevelt', 'No one can make you feel inferior without your consent.', NULL, @Now),
('Eleanor Roosevelt', 'Do one thing every day that scares you.', NULL, @Now),
('Eleanor Roosevelt', 'You gain strength, courage, and confidence by every experience in which you really stop to look fear in the face.', NULL, @Now),
('Eleanor Roosevelt', 'Great minds discuss ideas; average minds discuss events; small minds discuss people.', NULL, @Now),

-- Benjamin Franklin
('Benjamin Franklin', 'Tell me and I forget. Teach me and I remember. Involve me and I learn.', NULL, @Now),
('Benjamin Franklin', 'An investment in knowledge pays the best interest.', NULL, @Now),
('Benjamin Franklin', 'Well done is better than well said.', NULL, @Now),
('Benjamin Franklin', 'By failing to prepare, you are preparing to fail.', NULL, @Now),
('Benjamin Franklin', 'Early to bed and early to rise makes a man healthy, wealthy, and wise.', NULL, @Now),

-- Shakespeare
('William Shakespeare', 'All the world''s a stage, and all the men and women merely players.', NULL, @Now),
('William Shakespeare', 'To thine own self be true.', NULL, @Now),
('William Shakespeare', 'The course of true love never did run smooth.', NULL, @Now),
('William Shakespeare', 'What''s in a name? That which we call a rose by any other name would smell as sweet.', NULL, @Now),
('William Shakespeare', 'We know what we are, but know not what we may be.', NULL, @Now);
